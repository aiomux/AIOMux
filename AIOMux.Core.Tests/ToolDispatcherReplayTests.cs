using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;`nusing AIOMux.Core.Models.Agent;`nusing AIOMux.Core.Models.Events;`nusing AIOMux.Core.Models.Replay;
using AIOMux.Core.Replay;
using AIOMux.Core.Replay.Models;
using Xunit;

namespace AIOMux.Core.Tests;

public class ToolDispatcherReplayTests
{
    private class TestTool : ITool
    {
        private readonly Func<string, Task<string>> _implementation;

        public string Name { get; }

        public TestTool(string name, Func<string, Task<string>> implementation)
        {
            Name = name;
            _implementation = implementation;
        }

        public Task<string> ExecuteAsync(string input)
            => _implementation(input);
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

    [Fact]
    public async Task InvokeAsync_ReplayMode_Full_ReturnsRecordedResults()
    {
        // Arrange - Normal execution
        var normalEventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(normalEventSink);

        var tool = new TestTool("calculator", input => Task.FromResult($"calculated: {input}"));
        var context = new AgentContext();
        context.Tools.Add("calculator", tool);

        var call = new ToolCall
        {
            CallId = "call-123",
            ToolName = "calculator",
            JsonArgs = "{\"operation\":\"add\",\"a\":2,\"b\":3}"
        };

        // Act - Normal execution
        var normalResult = await dispatcher.InvokeAsync(call, context, CancellationToken.None);

        // Assert normal execution
        Assert.True(normalResult.Success);
        Assert.Equal("calculated: {\"operation\":\"add\",\"a\":2,\"b\":3}", normalResult.JsonResult);

        // Arrange - Replay execution
        var replayEventSink = new CapturingEventSink();
        var replayDispatcher = new ToolDispatcher(replayEventSink);

        var replaySource = new InMemoryReplaySource();
        replaySource.AddToolResult("call-123", normalResult);

        var replayContext = new AgentContext
        {
            ReplayMode = ReplayMode.Full,
            ReplaySource = replaySource
        };
        // Note: No tools added to replay context - should still work

        var replayCall = new ToolCall
        {
            CallId = "call-123",
            ToolName = "calculator",
            JsonArgs = "{\"operation\":\"add\",\"a\":2,\"b\":3}"
        };

        // Act - Replay execution
        var replayResult = await replayDispatcher.InvokeAsync(replayCall, replayContext, CancellationToken.None);

        // Assert replay execution
        Assert.True(replayResult.Success);
        Assert.Equal(normalResult.JsonResult, replayResult.JsonResult);
        Assert.Equal(normalResult.CallId, replayResult.CallId);

        // Verify event order for replay
        Assert.Collection(replayEventSink.Events,
            evt => Assert.Equal("ToolProposed", evt.Type),
            evt => Assert.Equal("PolicyEvaluated", evt.Type),
            evt => Assert.Equal("ToolExecuted", evt.Type),
            evt => Assert.Equal("ToolResult", evt.Type)
        );

        // Verify ToolExecuted indicates replay
        var toolExecutedEvent = replayEventSink.Events[2] as ToolExecutedEvent;
        Assert.NotNull(toolExecutedEvent);
        var payload = toolExecutedEvent.Payload as ToolExecutedEvent.ToolExecutedPayload;
        Assert.NotNull(payload);
        Assert.True(payload.FromReplay);
    }

    [Fact]
    public async Task InvokeAsync_ReplayMode_ToolsOnly_ReturnsRecordedResults()
    {
        // Arrange
        var normalResult = new ToolResult
        {
            CallId = "call-456",
            Success = true,
            JsonResult = "recorded-output"
        };

        var replaySource = new InMemoryReplaySource();
        replaySource.AddToolResult("call-456", normalResult);

        var eventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(eventSink);

        var context = new AgentContext
        {
            ReplayMode = ReplayMode.ToolsOnly,
            ReplaySource = replaySource
        };

        var call = new ToolCall
        {
            CallId = "call-456",
            ToolName = "some-tool",
            JsonArgs = "{}"
        };

        // Act
        var result = await dispatcher.InvokeAsync(call, context, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("recorded-output", result.JsonResult);
        Assert.Equal("call-456", result.CallId);
    }

    [Fact]
    public async Task InvokeAsync_ReplayMode_None_ExecutesToolNormally()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(eventSink);

        var executionCount = 0;
        var tool = new TestTool("counter", input =>
        {
            executionCount++;
            return Task.FromResult($"execution-{executionCount}");
        });

        var context = new AgentContext
        {
            ReplayMode = ReplayMode.None
        };
        context.Tools.Add("counter", tool);

        var call = new ToolCall
        {
            CallId = "call-789",
            ToolName = "counter",
            JsonArgs = "{}"
        };

        // Act
        var result = await dispatcher.InvokeAsync(call, context, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("execution-1", result.JsonResult);
        Assert.Equal(1, executionCount);

        // Verify ToolExecuted indicates not from replay
        var toolExecutedEvent = eventSink.Events[2] as ToolExecutedEvent;
        Assert.NotNull(toolExecutedEvent);
        var payload = toolExecutedEvent.Payload as ToolExecutedEvent.ToolExecutedPayload;
        Assert.NotNull(payload);
        Assert.False(payload.FromReplay);
    }

    [Fact]
    public async Task InvokeAsync_EventEmissionOrder_IsCorrect()
    {
        // Arrange
        var eventSink = new CapturingEventSink();
        var dispatcher = new ToolDispatcher(eventSink);

        var tool = new TestTool("test", input => Task.FromResult("output"));
        var context = new AgentContext();
        context.Tools.Add("test", tool);

        var call = new ToolCall
        {
            CallId = "call-order",
            ToolName = "test",
            JsonArgs = "{}"
        };

        // Act
        await dispatcher.InvokeAsync(call, context, CancellationToken.None);

        // Assert - Verify exact event order
        Assert.Equal(4, eventSink.Events.Count);
        Assert.Equal("ToolProposed", eventSink.Events[0].Type);
        Assert.Equal("PolicyEvaluated", eventSink.Events[1].Type);
        Assert.Equal("ToolExecuted", eventSink.Events[2].Type);
        Assert.Equal("ToolResult", eventSink.Events[3].Type);
    }
}

