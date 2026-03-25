using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core;

/// <summary>
/// Dispatches tool calls with optional policy checks, replay support, and event recording.
/// </summary>
public class ToolDispatcher : IToolDispatcher
{
    private readonly IRuntimeEventSink _eventSink;
    private readonly IPolicyEngine _policyEngine;

    /// <summary>
    /// Creates a new dispatcher for tool invocation.
    /// </summary>
    public ToolDispatcher(IRuntimeEventSink eventSink, IPolicyEngine? policyEngine = null)
    {
        _eventSink = eventSink;
        _policyEngine = policyEngine ?? new AllowAllPolicyEngine();
    }

    /// <summary>
    /// Invokes a tool call and records replay events.
    /// </summary>
    public async Task<ToolResult> InvokeAsync(
        ToolCall call,
        ExecutionContext context,
        CancellationToken ct = default)
    {
        // Validate cancellation before starting dispatch.
        ct.ThrowIfCancellationRequested();

        // Record the proposed tool call.
        await _eventSink.RecordAsync(
            new Replay.Models.ToolProposedEvent
            {
                Payload = new Replay.Models.ToolProposedEvent.ToolProposedPayload
                {
                    CallId = call.CallId,
                    ToolName = call.ToolName,
                    JsonArgs = call.JsonArgs
                }
            }, ct);

        // Evaluate policy for direct ToolDispatcher usage.
        // Policy is primarily evaluated at the step level in ExecutionRuntime.
        var stepMetadata = new ExecutionStepMetadata
        {
            StepId = call.CallId,
            Type = "tool",
            Target = call.ToolName
        };
        var policyDecision = _policyEngine.EvaluateStep(stepMetadata, new Dictionary<string, object?> { ["input"] = call.JsonArgs }, context);
        await _eventSink.RecordAsync(
            new Replay.Models.PolicyEvaluatedEvent
            {
                Payload = new Replay.Models.PolicyEvaluatedEvent.PolicyEvaluatedPayload
                {
                    CallId = call.CallId,
                    ToolName = call.ToolName,
                    Allowed = policyDecision.Allowed,
                    DenyReason = policyDecision.DenyReason,
                    PolicyHash = policyDecision.PolicyHash
                }
            }, ct);

        if (!policyDecision.Allowed)
        {
            var deniedResult = new ToolResult
            {
                CallId = call.CallId,
                Success = false,
                Error = policyDecision.DenyReason ?? "Tool call denied by policy.",
                JsonResult = string.Empty
            };
            await _eventSink.RecordAsync(new Replay.Models.ToolResultEvent { Result = deniedResult }, ct);
            return deniedResult;
        }

        // Resolve result from replay when available.
        ToolResult result;
        bool fromReplay = false;

        if ((context.ReplayMode == ReplayMode.Full || context.ReplayMode == ReplayMode.ToolsOnly)
            && context.ReplaySource != null
            && context.ReplaySource.TryGetToolResult(call.CallId, out var replayedResult))
        {
            result = replayedResult;
            fromReplay = true;
        }
        else
        {
            // Validate cancellation before tool execution.
            ct.ThrowIfCancellationRequested();

            if (!context.Tools.TryGetValue(call.ToolName, out var tool))
            {
                result = new ToolResult
                {
                    CallId = call.CallId,
                    Success = false,
                    Error = $"Tool not found: {call.ToolName}",
                    JsonResult = string.Empty
                };
            }
            else
            {
                try
                {
                    var output = await tool.ExecuteAsync(call.JsonArgs);
                    result = new ToolResult
                    {
                        CallId = call.CallId,
                        Success = true,
                        JsonResult = output
                    };
                }
                catch (Exception ex)
                {
                    result = new ToolResult
                    {
                        CallId = call.CallId,
                        Success = false,
                        Error = ex.Message,
                        JsonResult = string.Empty
                    };
                }
            }
        }

        // Record execution and final result events.
        await _eventSink.RecordAsync(
            new Replay.Models.ToolExecutedEvent
            {
                Payload = new Replay.Models.ToolExecutedEvent.ToolExecutedPayload
                {
                    CallId = call.CallId,
                    ToolName = call.ToolName,
                    FromReplay = fromReplay
                }
            }, ct);

        await _eventSink.RecordAsync(new Replay.Models.ToolResultEvent { Result = result }, ct);

        return result;
    }
}


