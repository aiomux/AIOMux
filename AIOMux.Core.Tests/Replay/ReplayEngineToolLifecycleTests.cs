using AIOMux.Core.Models;
using AIOMux.Core.Replay;
using AIOMux.Core.Replay.Models;
using System.Text.Json;
using Xunit;
using ToolResultEvent = AIOMux.Core.Replay.Models.ToolResultEvent;

namespace AIOMux.Core.Tests.Replay;

/// <summary>
/// Tests for ReplayEngine parsing and round-tripping of newer tool lifecycle events.
/// </summary>
public class ReplayEngineToolLifecycleTests : IDisposable
{
    private readonly string _testPath;
    private readonly RunRecorder _recorder;
    private readonly ReplayEngine _engine;

    public ReplayEngineToolLifecycleTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"aiomux-replay-lifecycle-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);
        _recorder = new RunRecorder(_testPath);
        _engine = new ReplayEngine(_testPath);
    }

    public void Dispose()
    {
        _recorder.Dispose();
        if (Directory.Exists(_testPath))
        {
            Directory.Delete(_testPath, recursive: true);
        }
    }

    [Fact]
    public async Task ReplayAsync_ParsesToolProposedEvent()
    {
        // Arrange
        var run = new Run { PipelineName = "TestPipeline" };
        await _recorder.StartRunAsync(run);

        var toolProposedEvent = new ToolProposedEvent
        {
            Payload = new ToolProposedEvent.ToolProposedPayload
            {
                CallId = "call-123",
                ToolName = "TestTool",
                JsonArgs = "{\"key\":\"value\"}"
            }
        };
        await _recorder.RecordEventAsync(toolProposedEvent);
        await _recorder.FinishRunAsync(success: true);

        // Act
        var result = await _engine.ReplayAsync(run.RunId);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.ToolCalls);
        Assert.Equal("call-123", result.ToolCalls[0].CallId);
        Assert.Equal("TestTool", result.ToolCalls[0].ToolName);
        Assert.Equal("{\"key\":\"value\"}", result.ToolCalls[0].Args);
    }

    [Fact]
    public async Task ReplayAsync_ParsesPolicyEvaluatedEvent()
    {
        // Arrange
        var run = new Run { PipelineName = "TestPipeline" };
        await _recorder.StartRunAsync(run);

        await _recorder.RecordEventAsync(new ToolProposedEvent
        {
            Payload = new ToolProposedEvent.ToolProposedPayload
            {
                CallId = "call-456",
                ToolName = "RestrictedTool",
                JsonArgs = "{}"
            }
        });

        var policyEvent = new PolicyEvaluatedEvent
        {
            Payload = new PolicyEvaluatedEvent.PolicyEvaluatedPayload
            {
                CallId = "call-456",
                ToolName = "RestrictedTool",
                Allowed = false,
                DenyReason = "Tool not allowed in production",
                PolicyHash = "policy-hash-123"
            }
        };
        await _recorder.RecordEventAsync(policyEvent);
        await _recorder.FinishRunAsync(success: true);

        // Act
        var result = await _engine.ReplayAsync(run.RunId);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.ToolCalls);
        Assert.Equal(false, result.ToolCalls[0].PolicyAllowed);
        Assert.Equal("Tool not allowed in production", result.ToolCalls[0].PolicyDenyReason);
    }

    [Fact]
    public async Task ReplayAsync_ParsesToolExecutedEvent()
    {
        // Arrange
        var run = new Run { PipelineName = "TestPipeline" };
        await _recorder.StartRunAsync(run);

        await _recorder.RecordEventAsync(new ToolProposedEvent
        {
            Payload = new ToolProposedEvent.ToolProposedPayload
            {
                CallId = "call-789",
                ToolName = "Calculator",
                JsonArgs = "{\"op\":\"add\"}"
            }
        });

        await _recorder.RecordEventAsync(new PolicyEvaluatedEvent
        {
            Payload = new PolicyEvaluatedEvent.PolicyEvaluatedPayload
            {
                CallId = "call-789",
                ToolName = "Calculator",
                Allowed = true,
                PolicyHash = "policy-hash-123"
            }
        });

        var toolExecutedEvent = new ToolExecutedEvent
        {
            Payload = new ToolExecutedEvent.ToolExecutedPayload
            {
                CallId = "call-789",
                ToolName = "Calculator",
                FromReplay = true
            }
        };
        await _recorder.RecordEventAsync(toolExecutedEvent);
        await _recorder.FinishRunAsync(success: true);

        // Act
        var result = await _engine.ReplayAsync(run.RunId);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.ToolCalls);
        Assert.True(result.ToolCalls[0].FromReplay);
    }

    [Fact]
    public async Task ReplayAsync_ParsesNewToolResultEvent()
    {
        // Arrange
        var run = new Run { PipelineName = "TestPipeline" };
        await _recorder.StartRunAsync(run);

        await _recorder.RecordEventAsync(new ToolProposedEvent
        {
            Payload = new ToolProposedEvent.ToolProposedPayload
            {
                CallId = "call-abc",
                ToolName = "DataFetcher",
                JsonArgs = "{\"query\":\"test\"}"
            }
        });

        await _recorder.RecordEventAsync(new PolicyEvaluatedEvent
        {
            Payload = new PolicyEvaluatedEvent.PolicyEvaluatedPayload
            {
                CallId = "call-abc",
                ToolName = "DataFetcher",
                Allowed = true,
                PolicyHash = "policy-hash-123"
            }
        });

        await _recorder.RecordEventAsync(new ToolExecutedEvent
        {
            Payload = new ToolExecutedEvent.ToolExecutedPayload
            {
                CallId = "call-abc",
                ToolName = "DataFetcher",
                FromReplay = false
            }
        });

        var toolResultEvent = new ToolResultEvent
        {
            Result = new ToolResult
            {
                CallId = "call-abc",
                Success = true,
                JsonResult = "{\"data\":\"fetched\"}"
            }
        };
        await _recorder.RecordEventAsync(toolResultEvent);
        await _recorder.FinishRunAsync(success: true);

        // Act
        var result = await _engine.ReplayAsync(run.RunId);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.ToolCalls);
        Assert.Equal("{\"data\":\"fetched\"}", result.ToolCalls[0].Result);
        Assert.True(result.ToolCalls[0].Success);
    }

    [Fact]
    public async Task ReplayAsync_RoundTripsCompleteToolLifecycle()
    {
        // Arrange - Create a complete tool lifecycle with all events
        var run = new Run { PipelineName = "CompletePipeline" };
        await _recorder.StartRunAsync(run);

        // ToolProposed
        await _recorder.RecordEventAsync(new ToolProposedEvent
        {
            Payload = new ToolProposedEvent.ToolProposedPayload
            {
                CallId = "call-complete",
                ToolName = "ComplexTool",
                JsonArgs = "{\"param1\":\"value1\",\"param2\":42}"
            }
        });

        // PolicyEvaluated
        await _recorder.RecordEventAsync(new PolicyEvaluatedEvent
        {
            Payload = new PolicyEvaluatedEvent.PolicyEvaluatedPayload
            {
                CallId = "call-complete",
                ToolName = "ComplexTool",
                Allowed = true,
                PolicyHash = "policy-hash-abc123"
            }
        });

        // ToolExecuted
        await _recorder.RecordEventAsync(new ToolExecutedEvent
        {
            Payload = new ToolExecutedEvent.ToolExecutedPayload
            {
                CallId = "call-complete",
                ToolName = "ComplexTool",
                FromReplay = false
            }
        });

        // ToolResult
        await _recorder.RecordEventAsync(new ToolResultEvent
        {
            Result = new ToolResult
            {
                CallId = "call-complete",
                Success = true,
                JsonResult = "{\"output\":\"result\",\"status\":\"completed\"}"
            }
        });

        await _recorder.FinishRunAsync(success: true, finalOutput: "All tools executed", totalDurationMs: 100.0);

        // Act - Replay the run
        var result = await _engine.ReplayAsync(run.RunId);

        // Assert - Verify all fields round-tripped correctly
        Assert.True(result.Success);
        Assert.Equal("CompletePipeline", result.PipelineName);
        Assert.Equal("All tools executed", result.FinalOutput);
        Assert.Single(result.ToolCalls);

        var toolCall = result.ToolCalls[0];
        Assert.Equal("call-complete", toolCall.CallId);
        Assert.Equal("ComplexTool", toolCall.ToolName);
        Assert.Equal("{\"param1\":\"value1\",\"param2\":42}", toolCall.Args);
        Assert.True(toolCall.PolicyAllowed);
        Assert.Null(toolCall.PolicyDenyReason);
        Assert.False(toolCall.FromReplay);
        Assert.Equal("{\"output\":\"result\",\"status\":\"completed\"}", toolCall.Result);
        Assert.True(toolCall.Success);
        Assert.Null(toolCall.Error);
    }

    [Fact]
    public async Task ReplayAsync_HandlesMultipleToolCallsInSequence()
    {
        // Arrange
        var run = new Run { PipelineName = "MultiToolPipeline" };
        await _recorder.StartRunAsync(run);

        // First tool call
        await _recorder.RecordEventAsync(new ToolProposedEvent
        {
            Payload = new ToolProposedEvent.ToolProposedPayload
            {
                CallId = "call-1",
                ToolName = "Tool1",
                JsonArgs = "{\"input\":\"first\"}"
            }
        });

        await _recorder.RecordEventAsync(new PolicyEvaluatedEvent
        {
            Payload = new PolicyEvaluatedEvent.PolicyEvaluatedPayload
            {
                CallId = "call-1",
                ToolName = "Tool1",
                Allowed = true,
                PolicyHash = "hash1"
            }
        });

        await _recorder.RecordEventAsync(new ToolExecutedEvent
        {
            Payload = new ToolExecutedEvent.ToolExecutedPayload
            {
                CallId = "call-1",
                ToolName = "Tool1",
                FromReplay = false
            }
        });

        await _recorder.RecordEventAsync(new ToolResultEvent
        {
            Result = new ToolResult
            {
                CallId = "call-1",
                Success = true,
                JsonResult = "result1"
            }
        });

        // Second tool call
        await _recorder.RecordEventAsync(new ToolProposedEvent
        {
            Payload = new ToolProposedEvent.ToolProposedPayload
            {
                CallId = "call-2",
                ToolName = "Tool2",
                JsonArgs = "{\"input\":\"second\"}"
            }
        });

        await _recorder.RecordEventAsync(new PolicyEvaluatedEvent
        {
            Payload = new PolicyEvaluatedEvent.PolicyEvaluatedPayload
            {
                CallId = "call-2",
                ToolName = "Tool2",
                Allowed = false,
                DenyReason = "Blocked by policy",
                PolicyHash = "hash2"
            }
        });

        await _recorder.RecordEventAsync(new ToolResultEvent
        {
            Result = new ToolResult
            {
                CallId = "call-2",
                Success = false,
                Error = "Blocked by policy"
            }
        });

        await _recorder.FinishRunAsync(success: true);

        // Act
        var result = await _engine.ReplayAsync(run.RunId);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.ToolCalls.Count);

        // First tool call
        Assert.Equal("call-1", result.ToolCalls[0].CallId);
        Assert.Equal("Tool1", result.ToolCalls[0].ToolName);
        Assert.True(result.ToolCalls[0].PolicyAllowed);
        Assert.True(result.ToolCalls[0].Success);
        Assert.Equal("result1", result.ToolCalls[0].Result);

        // Second tool call
        Assert.Equal("call-2", result.ToolCalls[1].CallId);
        Assert.Equal("Tool2", result.ToolCalls[1].ToolName);
        Assert.False(result.ToolCalls[1].PolicyAllowed);
        Assert.Equal("Blocked by policy", result.ToolCalls[1].PolicyDenyReason);
        Assert.False(result.ToolCalls[1].Success);
    }

    [Fact]
    public async Task ReplayAsync_SerializesAndDeserializesEventsCorrectly()
    {
        // Arrange - Create events and manually serialize/deserialize to test round-trip
        var events = new List<RuntimeEvent>
        {
            new ToolProposedEvent
            {
                RunId = "test-run",
                Seq = 0,
                TimestampUtc = DateTime.UtcNow,
                Payload = new ToolProposedEvent.ToolProposedPayload
                {
                    CallId = "call-serialize",
                    ToolName = "SerializeTool",
                    JsonArgs = "{\"test\":\"data\"}"
                }
            },
            new PolicyEvaluatedEvent
            {
                RunId = "test-run",
                Seq = 1,
                TimestampUtc = DateTime.UtcNow,
                Payload = new PolicyEvaluatedEvent.PolicyEvaluatedPayload
                {
                    CallId = "call-serialize",
                    ToolName = "SerializeTool",
                    Allowed = true,
                    PolicyHash = "hash123"
                }
            },
            new ToolExecutedEvent
            {
                RunId = "test-run",
                Seq = 2,
                TimestampUtc = DateTime.UtcNow,
                Payload = new ToolExecutedEvent.ToolExecutedPayload
                {
                    CallId = "call-serialize",
                    ToolName = "SerializeTool",
                    FromReplay = true
                }
            },
            new ToolResultEvent
            {
                RunId = "test-run",
                Seq = 3,
                TimestampUtc = DateTime.UtcNow,
                Result = new ToolResult
                {
                    CallId = "call-serialize",
                    Success = true,
                    JsonResult = "{\"result\":\"success\"}"
                }
            }
        };

        // Act - Serialize and deserialize through JSON
        var jsonLines = events.Select(e => JsonSerializer.Serialize(e)).ToList();

        // Use reflection to access private ParseEvent method
        var parseEventMethod = typeof(ReplayEngine).GetMethod("ParseEvent",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(parseEventMethod);

        var parsedEvents = new List<RuntimeEvent>();
        foreach (var json in jsonLines)
        {
            var parsed = parseEventMethod.Invoke(_engine, new object[] { json }) as RuntimeEvent;
            Assert.NotNull(parsed);
            parsedEvents.Add(parsed);
        }

        // Assert - Verify all events were parsed correctly
        Assert.Equal(4, parsedEvents.Count);
        Assert.Equal("ToolProposed", parsedEvents[0].Type);
        Assert.Equal("PolicyEvaluated", parsedEvents[1].Type);
        Assert.Equal("ToolExecuted", parsedEvents[2].Type);
        Assert.Equal("ToolResult", parsedEvents[3].Type);

        // Verify ToolProposed payload
        var toolProposed = parsedEvents[0] as ToolProposedEvent;
        Assert.NotNull(toolProposed);
        var proposedPayload = JsonSerializer.Deserialize<ToolProposedEvent.ToolProposedPayload>(
            JsonSerializer.Serialize(toolProposed.Payload));
        Assert.NotNull(proposedPayload);
        Assert.Equal("call-serialize", proposedPayload.CallId);
        Assert.Equal("SerializeTool", proposedPayload.ToolName);

        // Verify PolicyEvaluated payload
        var policyEvaluated = parsedEvents[1] as PolicyEvaluatedEvent;
        Assert.NotNull(policyEvaluated);
        var policyPayload = JsonSerializer.Deserialize<PolicyEvaluatedEvent.PolicyEvaluatedPayload>(
            JsonSerializer.Serialize(policyEvaluated.Payload));
        Assert.NotNull(policyPayload);
        Assert.True(policyPayload.Allowed);

        // Verify ToolExecuted payload
        var toolExecuted = parsedEvents[2] as ToolExecutedEvent;
        Assert.NotNull(toolExecuted);
        var executedPayload = JsonSerializer.Deserialize<ToolExecutedEvent.ToolExecutedPayload>(
            JsonSerializer.Serialize(toolExecuted.Payload));
        Assert.NotNull(executedPayload);
        Assert.True(executedPayload.FromReplay);

        // Verify ToolResult
        var toolResult = parsedEvents[3] as ToolResultEvent;
        Assert.NotNull(toolResult);
        Assert.NotNull(toolResult.Result);
        Assert.Equal("call-serialize", toolResult.Result.CallId);
        Assert.True(toolResult.Result.Success);
    }
}

