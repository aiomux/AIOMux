using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core;

public class ToolDispatcher : IToolDispatcher
{
    private readonly IRuntimeEventSink _eventSink;
    private readonly IPolicyEngine _policyEngine;

    public ToolDispatcher(IRuntimeEventSink eventSink, IPolicyEngine? policyEngine = null)
    {
        _eventSink = eventSink;
        _policyEngine = policyEngine ?? new AllowAllPolicyEngine();
    }

    public async Task<ToolResult> InvokeAsync(ToolCall call, ExecutionContext context, CancellationToken ct = default)
    {
        // Check cancellation at the start
        ct.ThrowIfCancellationRequested();

        // 1. ToolProposed event
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

        // 2. Policy evaluation
        var policyDecision = _policyEngine.Evaluate(call, context);
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
            await _eventSink.RecordAsync(
                new Replay.Models.ToolResultEvent
                {
                    Result = deniedResult
                }, ct);
            return deniedResult;
        }

        // 3. Check replay mode
        ToolResult result;
        bool fromReplay = false;

        if ((context.ReplayMode == ReplayMode.Full || context.ReplayMode == ReplayMode.ToolsOnly)
            && context.ReplaySource != null
            && context.ReplaySource.TryGetToolResult(call.CallId, out var replayedResult))
        {
            // Use recorded result from replay
            result = replayedResult;
            fromReplay = true;
        }
        else
        {
            // Check cancellation before executing tool
            ct.ThrowIfCancellationRequested();

            // Execute tool normally
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

        // 4. ToolExecuted event
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

        // 5. ToolResult event
        await _eventSink.RecordAsync(
            new Replay.Models.ToolResultEvent
            {
                Result = result
            }, ct);

        return result;
    }
}


