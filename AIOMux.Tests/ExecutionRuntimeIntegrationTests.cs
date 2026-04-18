using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
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

    private static string Sha256(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }

    private sealed class UppercaseTool : ITool
    {
        public string Name => "upper";
        public IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        public Task<string> ExecuteAsync(string input) => Task.FromResult(input.ToUpperInvariant());
    }

    private sealed class ThrowIfCalledTool : ITool
    {
        public string Name => "lookup";
        public bool WasCalled { get; private set; }
        public IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        public Task<string> ExecuteAsync(string input)
        {
            WasCalled = true;
            throw new InvalidOperationException("Tool should not be called while in replay mode");
        }
    }

    private sealed class DenyAllPolicyEngine(string reason, string hash) : IPolicyEngine
    {
        public PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context)
            => PolicyDecision.Deny(reason, hash);
    }
}
