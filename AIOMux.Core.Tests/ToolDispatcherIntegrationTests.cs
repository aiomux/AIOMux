using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;`nusing AIOMux.Core.Models.Agent;`nusing AIOMux.Core.Models.Events;`nusing AIOMux.Core.Models.Replay;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using AIOMux.Core.Replay.Models;
using Xunit;

namespace AIOMux.Core.Tests;

/// <summary>
/// Tests verifying that all tool execution flows through ToolDispatcher with proper event emission and policy enforcement.
/// </summary>
public class ToolDispatcherIntegrationTests
{
    private class TestTool : ITool, IAgent
    {
        public string Name { get; }
        public int ExecutionCount { get; private set; }

        public TestTool(string name)
        {
            Name = name;
        }

        public Task<string> ExecuteAsync(string input)
        {
            ExecutionCount++;
            return Task.FromResult($"{Name} processed: {input}");
        }

        public Task<string> ExecuteAsync(AgentContext context)
        {
            return ExecuteAsync(context.UserInput);
        }
    }

    private class CapturingEventSink : IRuntimeEventSink
    {
        public List<RuntimeEvent> Events { get; } = new();

        public Task RecordAsync(RuntimeEvent evt, CancellationToken ct)
        {
            Events.Add(evt);
            return Task.CompletedTask;
        }
    }

    private class DenyAllPolicyEngine : IPolicyEngine
    {
        public PolicyDecision Evaluate(ToolCall call, AgentContext context)
        {
            return PolicyDecision.Deny("All tools denied by test policy", "deny-all-hash");
        }
    }

    [Fact]
    public async Task AgentContext_ExecuteToolAsync_FlowsThroughToolDispatcher()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var policyEngine = new AllowAllPolicyEngine();
        var dispatcher = new ToolDispatcher(eventSink, policyEngine);

        var context = new AgentContext
        {
            ToolDispatcher = dispatcher
        };

        var tool = new TestTool("calculator");
        context.Tools.Add("calculator", tool);

        // Act
        var result = await context.ExecuteToolAsync("calculator", "{\"op\":\"add\"}");

        // Assert
        Assert.True(result.Success);
        Assert.Contains("calculator processed:", result.JsonResult);

        // Verify event emission order
        Assert.Equal(4, eventSink.Events.Count);
        Assert.Equal("ToolProposed", eventSink.Events[0].Type);
        Assert.Equal("PolicyEvaluated", eventSink.Events[1].Type);
        Assert.Equal("ToolExecuted", eventSink.Events[2].Type);
        Assert.Equal("ToolResult", eventSink.Events[3].Type);
    }

    [Fact]
    public async Task AgentContext_ExecuteToolAsync_PolicyDenyPreventsExecution()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var policyEngine = new DenyAllPolicyEngine();
        var dispatcher = new ToolDispatcher(eventSink, policyEngine);

        var context = new AgentContext
        {
            ToolDispatcher = dispatcher
        };

        var tool = new TestTool("calculator");
        context.Tools.Add("calculator", tool);

        // Act
        var result = await context.ExecuteToolAsync("calculator", "{\"op\":\"add\"}");

        // Assert - Tool execution was denied
        Assert.False(result.Success);
        Assert.Contains("denied", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, tool.ExecutionCount); // Tool was NOT executed

        // Verify events still emitted
        Assert.True(eventSink.Events.Count >= 2);
        Assert.Equal("ToolProposed", eventSink.Events[0].Type);
        Assert.Equal("PolicyEvaluated", eventSink.Events[1].Type);

        // Verify PolicyEvaluated indicates denial
        var policyEvent = eventSink.Events[1] as PolicyEvaluatedEvent;
        Assert.NotNull(policyEvent);
        var payload = policyEvent.Payload as PolicyEvaluatedEvent.PolicyEvaluatedPayload;
        Assert.NotNull(payload);
        Assert.False(payload.Allowed);
        Assert.Equal("calculator", payload.ToolName);
    }

    [Fact]
    public async Task AgentContext_WithDefaultDispatcher_StillWorks()
    {
        // Arrange - Use default dispatcher (no explicit setup)
        var context = new AgentContext(); // Default dispatcher is lazy-created

        var tool = new TestTool("echo");
        context.Tools.Add("echo", tool);

        // Act
        var result = await context.ExecuteToolAsync("echo", "test input");

        // Assert
        Assert.True(result.Success);
        Assert.Contains("echo processed:", result.JsonResult);
        Assert.Equal(1, tool.ExecutionCount);
    }

    [Fact]
    public async Task ToolExecution_RunAsync_WithTool_FlowsThroughDispatcher()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(eventSink);

        var context = new AgentContext
        {
            ToolDispatcher = dispatcher,
            UserInput = "test input"
        };

        var tool = new TestTool("processor");
        context.Tools.Add("processor", tool);

        // Act - Cast to IAgent since ToolExecution.RunAsync expects IAgent
        var output = await ToolExecution.RunAsync((IAgent)tool, context);

        // Assert
        Assert.Contains("processor processed:", output);
        Assert.Equal(1, tool.ExecutionCount);

        // Verify events were emitted
        Assert.Equal(4, eventSink.Events.Count);
        Assert.Equal("ToolProposed", eventSink.Events[0].Type);
        Assert.Equal("PolicyEvaluated", eventSink.Events[1].Type);
        Assert.Equal("ToolExecuted", eventSink.Events[2].Type);
        Assert.Equal("ToolResult", eventSink.Events[3].Type);
    }

    [Fact]
    public async Task ToolExecution_RunWithMetricsAsync_WithTool_FlowsThroughDispatcher()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(eventSink);

        var context = new AgentContext
        {
            ToolDispatcher = dispatcher,
            UserInput = "metric test"
        };

        var tool = new TestTool("analyzer");
        context.Tools.Add("analyzer", tool);

        // Act - Cast to IAgent since ToolExecution.RunWithMetricsAsync expects IAgent
        var (result, metrics) = await ToolExecution.RunWithMetricsAsync((IAgent)tool, context);

        // Assert
        Assert.Contains("analyzer processed:", result);
        Assert.Equal(1, tool.ExecutionCount);

        // Verify events were emitted
        Assert.Equal(4, eventSink.Events.Count);
    }

    [Fact]
    public async Task ToolNotFound_ReturnsFailedResultWithoutCrashing()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(eventSink);

        var context = new AgentContext
        {
            ToolDispatcher = dispatcher
        };

        // Act - Try to execute a tool that doesn't exist
        var result = await context.ExecuteToolAsync("nonexistent", "{}");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);

        // No events should be emitted for tool not found (happens before dispatcher)
        Assert.Empty(eventSink.Events);
    }

    [Fact]
    public async Task ToolDispatcher_EventsContainCorrectCallId()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(eventSink);

        var context = new AgentContext
        {
            ToolDispatcher = dispatcher
        };

        var tool = new TestTool("tester");
        context.Tools.Add("tester", tool);

        // Act
        var result = await context.ExecuteToolAsync("tester", "{}");

        // Assert
        Assert.True(result.Success);

        // Extract CallId from each event and verify they're consistent
        var proposedEvent = eventSink.Events[0] as ToolProposedEvent;
        var policyEvent = eventSink.Events[1] as PolicyEvaluatedEvent;
        var executedEvent = eventSink.Events[2] as ToolExecutedEvent;
        var resultEvent = eventSink.Events[3] as AIOMux.Core.Replay.Models.ToolResultEvent;

        Assert.NotNull(proposedEvent);
        Assert.NotNull(policyEvent);
        Assert.NotNull(executedEvent);
        Assert.NotNull(resultEvent);

        var proposedPayload = proposedEvent.Payload as ToolProposedEvent.ToolProposedPayload;
        var policyPayload = policyEvent.Payload as PolicyEvaluatedEvent.PolicyEvaluatedPayload;
        var executedPayload = executedEvent.Payload as ToolExecutedEvent.ToolExecutedPayload;

        Assert.NotNull(proposedPayload);
        Assert.NotNull(policyPayload);
        Assert.NotNull(executedPayload);

        // All events should have the same CallId
        Assert.Equal(proposedPayload.CallId, policyPayload.CallId);
        Assert.Equal(proposedPayload.CallId, executedPayload.CallId);
        Assert.Equal(proposedPayload.CallId, resultEvent.Result?.CallId);

        // CallId should match the result
        Assert.Equal(result.CallId, proposedPayload.CallId);
    }

    [Fact]
    public async Task CancellationToken_PropagatedThroughDispatcher()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(eventSink);

        var context = new AgentContext
        {
            ToolDispatcher = dispatcher
        };

        var tool = new TestTool("slow-tool");
        context.Tools.Add("slow-tool", tool);

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await context.ExecuteToolAsync("slow-tool", "{}", cts.Token);
        });
    }
}

