using AIOMux.Core;
using AIOMux.Core.Dispatch;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests;

public sealed class ExecutionRuntimeIntegrationTests
{
    [Fact]
    public async Task ExecuteAsync_ToolThenAgentPipeline_CompletesSuccessfully()
    {
        var agentManager = new AgentManager();
        agentManager.Register(new EchoAgent());

        var services = new ExecutionRuntimeServices
        {
            AgentManager = agentManager,
            Tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
            {
                ["upper"] = new UppercaseTool()
            },
            PolicyEngine = new AllowAllPolicyEngine(),
            Options = new ExecutionOptions
            {
                CollectMetrics = false,
                GenerateJobSummary = false
            }
        };

        var plan = new ExecutionPlan
        {
            Name = "pipeline",
            Steps =
            [
                new ExecutionStep
                {
                    Id = "tool1",
                    Type = "tool",
                    Target = "upper",
                    Bindings = new Dictionary<string, string> { ["input"] = "inputs.input" }
                },
                new ExecutionStep
                {
                    Id = "agent1",
                    Type = "agent",
                    Target = "echo",
                    Bindings = new Dictionary<string, string> { ["input"] = "state.upper" }
                }
            ]
        };

        var context = new ExecutionContext
        {
            Services = services,
            Inputs = new Dictionary<string, object?> { ["input"] = "hello" }
        };

        var runtime = new ExecutionRuntime();
        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success, result.Error);
        Assert.Equal("HELLO", result.Output);
        Assert.Equal(2, context.Records.Count);
        Assert.Equal("HELLO", context.State["upper"]);
        Assert.Equal("HELLO", context.State["echo"]);
        Assert.Contains("- echo", context.State["AvailableAgents"]?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPolicyDeniesStep_ReturnsFailureWithPolicyDetails()
    {
        var services = new ExecutionRuntimeServices
        {
            PolicyEngine = new DenyAllPolicyEngine("blocked by policy", "policy-v1"),
            Options = new ExecutionOptions
            {
                CollectMetrics = false,
                GenerateJobSummary = false
            }
        };

        var plan = new ExecutionPlan
        {
            Name = "denied",
            Steps = [new ExecutionStep { Id = "step1", Type = "tool", Target = "missing" }]
        };

        var context = new ExecutionContext { Services = services };

        var runtime = new ExecutionRuntime();
        var result = await runtime.ExecuteAsync(plan, context);

        Assert.False(result.Success);
        Assert.Equal("blocked by policy", result.Error);
        Assert.Single(context.Records);
        Assert.Equal("blocked by policy", context.Records[0].PolicyDenyReason);
        Assert.Equal("policy-v1", context.Records[0].PolicyHash);
    }

    [Fact]
    public async Task ExecuteAsync_WithToolReplayMode_UsesRecordedToolResult()
    {
        var tool = new ThrowIfCalledTool();
        var replayInput = "replay-input";
        var replayKey = Sha256("0:lookup:replay-input");

        var services = new ExecutionRuntimeServices
        {
            Tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
            {
                ["lookup"] = tool
            },
            PolicyEngine = new AllowAllPolicyEngine(),
            ReplayMode = ReplayMode.ToolsOnly,
            ReplayToolResults = new Dictionary<string, ToolResult>(StringComparer.Ordinal)
            {
                [replayKey] = new ToolResult
                {
                    Success = true,
                    JsonResult = "from-replay"
                }
            },
            Options = new ExecutionOptions
            {
                CollectMetrics = false,
                GenerateJobSummary = false
            }
        };

        var plan = new ExecutionPlan
        {
            Name = "replay",
            Steps =
            [
                new ExecutionStep
                {
                    Id = "step1",
                    Type = "tool",
                    Target = "lookup",
                    Inputs = new Dictionary<string, object?> { ["input"] = replayInput }
                }
            ]
        };

        var context = new ExecutionContext { Services = services };

        var runtime = new ExecutionRuntime();
        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success, result.Error);
        Assert.Equal("from-replay", result.Output);
        Assert.False(tool.WasCalled);
    }

    [Fact]
    public async Task ExecuteAsync_AgentStep_RecordsSelectedLlmProfileName()
    {
        var agent = new PreferredProfileAgent();
        var agentManager = new AgentManager();
        agentManager.Register(agent);

        var resolver = new LLMClientResolver(
        [
            new KeyValuePair<string, ILLMClient>("coding-local", new FakeLlmClient("ollama", "qwen2.5-coder")),
            new KeyValuePair<string, ILLMClient>("default", new FakeLlmClient("ollama", "llama3"))
        ]);

        var services = new ExecutionRuntimeServices
        {
            AgentManager = agentManager,
            LlmClientResolver = resolver,
            PolicyEngine = new AllowAllPolicyEngine(),
            Options = new ExecutionOptions
            {
                CollectMetrics = false,
                GenerateJobSummary = false
            }
        };

        var plan = new ExecutionPlan
        {
            Name = "agent-profile",
            Steps =
            [
                new ExecutionStep
                {
                    Id = "agent-step",
                    Type = "agent",
                    Target = "preferred"
                }
            ]
        };

        var context = new ExecutionContext
        {
            Services = services,
            Inputs = new Dictionary<string, object?> { ["input"] = "hello" }
        };

        var runtime = new ExecutionRuntime();
        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success, result.Error);
        var record = Assert.Single(context.Records);
        Assert.Equal("coding-local", record.LlmProfile);
    }

    [Fact]
    public async Task ExecuteAsync_ToolStep_RecordsAnalyzedTargets()
    {
        var services = new ExecutionRuntimeServices
        {
            Tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
            {
                ["targeted"] = new TargetedTool()
            },
            PolicyEngine = new AllowAllPolicyEngine(),
            Options = new ExecutionOptions
            {
                CollectMetrics = false,
                GenerateJobSummary = false
            }
        };

        var plan = new ExecutionPlan
        {
            Name = "target-record",
            Steps =
            [
                new ExecutionStep
                {
                    Id = "tool-step",
                    Type = "tool",
                    Target = "targeted",
                    Inputs = new Dictionary<string, object?>
                    {
                        ["input"] = "C:/temp/aiomux/file.txt"
                    }
                }
            ]
        };

        var context = new ExecutionContext { Services = services };

        var runtime = new ExecutionRuntime();
        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success, result.Error);
        var record = Assert.Single(context.Records);
        var target = Assert.Single(record.ToolTargets);
        Assert.Equal(ToolTargetKind.FilePath, target.Kind);
        Assert.Equal("C:/temp/aiomux/file.txt", target.Value);
    }

    private static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    private sealed class UppercaseTool : DispatchableToolBase
    {
        public override string Name => "upper";
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input) => Task.FromResult(input.ToUpperInvariant());
    }

    private sealed class ThrowIfCalledTool : DispatchableToolBase
    {
        public override string Name => "lookup";
        public bool WasCalled { get; private set; }
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input)
        {
            WasCalled = true;
            throw new InvalidOperationException("Tool should not be called while in replay mode");
        }
    }

    private sealed class DenyAllPolicyEngine(string reason, string hash) : IPolicyEngine
    {
        public string PolicyType => "denyall";
        public PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context)
            => PolicyDecision.Deny(reason, hash);
    }

    private sealed class TargetedTool : DispatchableToolBase
    {
        public override string Name => "targeted";
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) =>
            ToolExecutionAnalysis.Recognized([ToolOperation.Read], [new ToolTarget(ToolTargetKind.FilePath, input)]);
        protected override Task<string> InvokeCoreAsync(string input) => Task.FromResult("ok");
    }

    private sealed class PreferredProfileAgent : IAgent
    {
        public string Name => "preferred";

        public AgentMetadata Metadata => new()
        {
            Name = Name,
            Description = "test",
            PreferredLlmProfile = "coding-local"
        };

        public Task<StepExecutionResult> ExecuteAsync(
            ImmutableDictionary<string, object?> inputs,
            ExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new StepExecutionResult { Success = true, Output = "ok" });
    }

    private sealed class FakeLlmClient(string provider, string model) : ILLMClient
    {
        public string Provider => provider;
        public string Model => model;
        public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
            => Task.FromResult(prompt);
        public Task<string> CompleteAsync(string userInput, string systemPrompt, CancellationToken cancellationToken = default)
            => Task.FromResult(userInput);
    }
}
