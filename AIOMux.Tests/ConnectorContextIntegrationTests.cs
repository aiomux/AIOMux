using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests;

public sealed class ConnectorContextIntegrationTests
{
    [Fact]
    public async Task PublishAsync_UsesDefaultPlanAndCopiesConnectorMetadata()
    {
        var runtime = new RecordingRuntime();
        var plan = new ExecutionPlan
        {
            Name = "main",
            Steps = [new ExecutionStep { Id = "a1", Type = "agent", Target = "echo" }]
        };

        ConnectorEvent? callbackEvent = null;
        ExecutionResult? callbackResult = null;

        var context = new ConnectorContext(
            runtime,
            plan,
            new ExecutionRuntimeServices(),
            config: new Dictionary<string, string> { ["channel"] = "alerts" },
            onExecutionCompleted: (evt, result) =>
            {
                callbackEvent = evt;
                callbackResult = result;
            });

        var evt = new ConnectorEvent
        {
            ConnectorName = "console",
            EventType = "console.line",
            Payload = "hello",
            Metadata = new Dictionary<string, object> { ["tenant"] = "alpha" }
        };

        await context.PublishAsync(evt);

        Assert.Same(plan, runtime.LastPlan);
        Assert.Equal("hello", runtime.LastContext?.Inputs["input"]);
        Assert.Equal("console", runtime.LastContext?.State["connector.name"]);
        Assert.Equal("console.line", runtime.LastContext?.State["connector.eventType"]);
        Assert.Equal("alpha", runtime.LastContext?.State["tenant"]);
        Assert.Equal("alerts", context.Config["channel"]);
        Assert.Same(evt, callbackEvent);
        Assert.NotNull(callbackResult);
        Assert.True(callbackResult!.Success);
    }

    [Fact]
    public async Task PublishAsync_WithEntryAgentOverride_BuildsSingleAgentPlan()
    {
        var runtime = new RecordingRuntime();
        var context = new ConnectorContext(
            runtime,
            new ExecutionPlan
            {
                Name = "main",
                Steps = [new ExecutionStep { Id = "a1", Type = "agent", Target = "echo" }]
            },
            new ExecutionRuntimeServices());

        await context.PublishAsync(new ConnectorEvent
        {
            ConnectorName = "console",
            EventType = "line",
            Payload = "payload",
            EntryAgent = "custom-agent"
        });

        Assert.NotNull(runtime.LastPlan);
        Assert.Equal("connector-custom-agent", runtime.LastPlan!.Name);
        Assert.Single(runtime.LastPlan.Steps);
        Assert.Equal("custom-agent", runtime.LastPlan.Steps[0].Target);
        Assert.Equal("payload", runtime.LastContext?.Inputs["input"]);
    }

    private sealed class RecordingRuntime : IExecutionRuntime
    {
        public ExecutionPlan? LastPlan { get; private set; }

        public ExecutionContext? LastContext { get; private set; }

        public Task<ExecutionResult> ExecuteAsync(
            ExecutionPlan plan,
            ExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            LastPlan = plan;
            LastContext = context;
            return Task.FromResult(new ExecutionResult
            {
                Success = true,
                Output = context.GetInput()
            });
        }
    }
}
