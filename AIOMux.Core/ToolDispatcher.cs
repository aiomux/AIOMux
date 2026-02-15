using System.Threading;
using System.Threading.Tasks;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core;

public class ToolDispatcher
{
    private readonly IRuntimeEventSink _eventSink;
    private readonly IPolicyEngine _policyEngine;

    public ToolDispatcher(IRuntimeEventSink eventSink, IPolicyEngine? policyEngine = null)
    {
        _eventSink = eventSink;
        _policyEngine = policyEngine ?? new AllowAllPolicyEngine();
    }

    public async Task<ToolResult> InvokeAsync(ToolCall call, AgentContext context, CancellationToken ct = default)
    {
        // Policy evaluation
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

        // Existing tool execution logic (placeholder, to be replaced with actual logic)
        // ...
        // For demonstration, return a successful result
        var result = new ToolResult
        {
            CallId = call.CallId,
            Success = true,
            JsonResult = "{}"
        };
        await _eventSink.RecordAsync(
            new Replay.Models.ToolResultEvent
            {
                Result = result
            }, ct);
        return result;
    }
}
