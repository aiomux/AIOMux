using AIOMux.Core.Dispatch;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using System.Text.Json;

namespace AIOMux.Tests;

public sealed class ToolDescriptorMetadataTests
{
    [Fact]
    public void DispatchableToolBase_ProvidesFallbackDescriptor()
    {
        var tool = new MinimalTool();

        var descriptor = tool.Descriptor;

        Assert.NotNull(descriptor);
        Assert.Equal("minimal", descriptor.Name);
        Assert.NotEmpty(descriptor.Operations);
    }

    [Fact]
    public void DescriptorOperations_MatchSupportedOperations_ForFallbackDescriptor()
    {
        var tool = new MultiOperationTool();

        var supported = tool.SupportedOperations.OrderBy(o => o).ToArray();
        var described = tool.Descriptor.Operations
            .Select(o => o.Operation)
            .OrderBy(o => o)
            .ToArray();

        Assert.Equal(supported, described);
    }

    [Fact]
    public void FallbackDescriptor_ExposesRequestAndResponseClrTypes()
    {
        var tool = new MinimalTool();
        var operation = Assert.Single(tool.Descriptor.Operations);

        Assert.Equal(typeof(string), operation.RequestType);
        Assert.Equal(typeof(string), operation.ResponseType);
        Assert.False(string.IsNullOrWhiteSpace(operation.RequestSchemaJson));
        Assert.False(string.IsNullOrWhiteSpace(operation.ResponseSchemaJson));
    }

    [Fact]
    public void DescriptorDebugView_SerializesSafeTypeNames_ByDefault()
    {
        var tool = new MinimalTool();

        var debugView = ToolDescriptorMetadataSerializer.ToDebugView(tool.Descriptor);
        var json = JsonSerializer.Serialize(debugView);

        Assert.Contains("System.String", json, StringComparison.Ordinal);
        Assert.DoesNotContain(", Version=", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispatcher_ExposesToolDescriptorCatalog()
    {
        var tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
        {
            ["minimal"] = new MinimalTool(),
            ["multi"] = new MultiOperationTool()
        };

        var dispatcher = new ToolDispatcher(tools, new AllowAllPolicyEngine());

        Assert.Equal(2, dispatcher.ToolCatalog.Count);
        Assert.Contains("minimal", dispatcher.ToolCatalog.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("multi", dispatcher.ToolCatalog.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dispatcher_UsesSafeFallbackDescriptor_WhenToolDescriptorIsNull()
    {
        var tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
        {
            ["legacy"] = new LegacyToolWithNullDescriptor()
        };

        var dispatcher = new ToolDispatcher(tools, new AllowAllPolicyEngine());

        var descriptor = dispatcher.ToolCatalog["legacy"];
        Assert.Equal("legacy", descriptor.Name);
        Assert.Single(descriptor.Operations);
        Assert.Equal(ToolOperation.Read, descriptor.Operations[0].Operation);
        Assert.Equal(typeof(string), descriptor.Operations[0].RequestType);
        Assert.Equal(typeof(string), descriptor.Operations[0].ResponseType);
    }

    [Fact]
    public async Task RuntimeExecutionPath_RemainsDispatcherPolicyAndInvokeFlow()
    {
        var trackingTool = new TrackingTool();
        var policy = new CountingPolicyEngine();

        var services = new ExecutionRuntimeServices
        {
            Tools = new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
            {
                ["tracking"] = trackingTool
            },
            PolicyEngine = policy,
            Options = new ExecutionOptions
            {
                CollectMetrics = false,
                GenerateJobSummary = false
            }
        };

        var plan = new ExecutionPlan
        {
            Name = "flow",
            Steps =
            [
                new ExecutionStep
                {
                    Id = "s1",
                    Type = "tool",
                    Target = "tracking",
                    Inputs = new Dictionary<string, object?>
                    {
                        ["input"] = "hello"
                    }
                }
            ]
        };

        var context = new AIOMux.Core.Models.ExecutionContext { Services = services };
        var runtime = new AIOMux.Core.ExecutionRuntime();

        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success, result.Error);
        Assert.Equal(1, policy.EvaluateCalls);
        Assert.Equal(1, trackingTool.AnalyzeCalls);
        Assert.Equal(1, trackingTool.InvokeCalls);
    }

    private sealed class MinimalTool : DispatchableToolBase
    {
        public override string Name => "minimal";
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input) => Task.FromResult(input);
    }

    private sealed class MultiOperationTool : DispatchableToolBase
    {
        public override string Name => "multi";
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read, ToolOperation.Write];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input) => Task.FromResult(input);
    }

    private sealed class LegacyToolWithNullDescriptor : ITool
    {
        public string Name => "legacy";
        public ToolDescriptor Descriptor => null!;
        public IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
    }

    private sealed class TrackingTool : DispatchableToolBase
    {
        public override string Name => "tracking";
        public int AnalyzeCalls { get; private set; }
        public int InvokeCalls { get; private set; }
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];

        public override ToolExecutionAnalysis Analyze(string input)
        {
            AnalyzeCalls++;
            return ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        }

        protected override Task<string> InvokeCoreAsync(string input)
        {
            InvokeCalls++;
            return Task.FromResult(input);
        }
    }

    private sealed class CountingPolicyEngine : IPolicyEngine
    {
        public string PolicyType => "counting";
        public int EvaluateCalls { get; private set; }

        public PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context)
        {
            EvaluateCalls++;
            return PolicyDecision.Allow("test-hash");
        }
    }
}
