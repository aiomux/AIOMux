using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AIOMux.Core.Dispatch;

/// <summary>
/// Single execution choke point for all tool invocations.
///
/// Every call to <see cref="InvokeAsync"/> follows a strict linear sequence:
///   1. Emit <see cref="ToolProposedEvent"/>.
///   2. Resolve the tool; synthesize an unrecognized analysis when the tool is absent.
///   3. Call <c>ITool.Analyze</c> to classify requested operations.
///   4. Call <see cref="IPolicyEngine.Evaluate"/>; emit <see cref="PolicyEvaluatedEvent"/>.
///   5. If denied: return with <see cref="ToolDispatchResult.PolicyDenied"/> set.
///   6. If replay cache hit after policy approval: emit <see cref="ToolResultEvent"/> and return.
///   7. Call dispatcher-owned execution path; emit <see cref="ToolExecutedEvent"/>.
///   8. Emit <see cref="ToolResultEvent"/> and return.
/// </summary>
public sealed class ToolDispatcher
{
    private readonly IReadOnlyDictionary<string, ITool> _tools;
    private readonly IPolicyEngine _policy;
    private readonly ReplayMode _replayMode;
    private readonly IReadOnlyDictionary<string, ToolResult> _replayCache;
    private readonly ILogger? _logger;

    public ToolDispatcher(
        IReadOnlyDictionary<string, ITool> tools,
        IPolicyEngine policy,
        ReplayMode replayMode = ReplayMode.None,
        IReadOnlyDictionary<string, ToolResult>? replayCache = null,
        ILogger? logger = null)
    {
        _tools = tools;
        _policy = policy;
        _replayMode = replayMode;
        _replayCache = replayCache ?? new Dictionary<string, ToolResult>(StringComparer.Ordinal);
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a single tool invocation through the full analyze/policy/execute pipeline.
    /// </summary>
    /// <param name="toolName">Registered name of the tool to invoke.</param>
    /// <param name="input">Raw input forwarded to <c>Analyze</c> and <c>ExecuteAsync</c>.</param>
    /// <param name="callId">Deterministic identifier for this call, used in all emitted events.</param>
    /// <param name="replayKey">Cache key for replay lookup.</param>
    /// <param name="agentContext">Minimal execution context forwarded to the policy engine.</param>
    /// <param name="ct">Cancellation token checked before execution begins.</param>
    public async Task<ToolDispatchResult> InvokeAsync(
        string toolName,
        string input,
        string callId,
        string replayKey,
        AgentContext agentContext,
        CancellationToken ct = default)
    {
        var events = new List<ToolDispatchEvent>();

        ToolResult? cachedResult = null;
        bool isReplayed = (_replayMode == ReplayMode.Full || _replayMode == ReplayMode.ToolsOnly)
            && _replayCache.TryGetValue(replayKey, out cachedResult);

        events.Add(new ToolProposedEvent
        {
            CallId = callId,
            ToolName = toolName,
            Input = input,
            IsReplayed = isReplayed
        });

        // Resolve and analyze before every policy evaluation.
        _tools.TryGetValue(toolName, out var tool);

        var analysis = tool != null
            ? tool.Analyze(input)
            : ToolExecutionAnalysis.Unrecognized($"Tool not found: {toolName}");

        var toolCall = new ToolCall { ToolName = toolName, Input = input };
        var decision = _policy.Evaluate(toolCall, analysis, agentContext);

        events.Add(new PolicyEvaluatedEvent
        {
            CallId = callId,
            ToolName = toolName,
            RequestedOperations = analysis.RequestedOperations,
            IsRecognized = analysis.IsRecognized,
            Allowed = decision.Allowed,
            Reason = decision.DenyReason,
            PolicyHash = decision.PolicyHash
        });

        if (!decision.Allowed)
        {
            _logger?.LogWarning(
                "Tool '{ToolName}' denied [{PolicyHash}]: {Reason}",
                toolName, decision.PolicyHash, decision.DenyReason);

            return new ToolDispatchResult
            {
                ToolResult = new ToolResult { CallId = callId, Success = false, Error = decision.DenyReason },
                PolicyDenied = true,
                PolicyDenyReason = decision.DenyReason,
                PolicyHash = decision.PolicyHash,
                Events = events
            };
        }

        if (tool is not DispatchableToolBase dispatchableTool)
        {
            var reason = $"Tool '{toolName}' must inherit DispatchableToolBase to execute through ToolDispatcher.";
            return new ToolDispatchResult
            {
                ToolResult = new ToolResult { CallId = callId, Success = false, Error = reason },
                PolicyDenied = true,
                PolicyDenyReason = reason,
                PolicyHash = decision.PolicyHash,
                Events = events
            };
        }

        // Replay path after policy approval: skip execution but keep policy evaluation mandatory.
        if (isReplayed)
        {
            var replayToolResult = new ToolResult
            {
                CallId = callId,
                Success = cachedResult!.Success,
                Error = cachedResult.Error,
                JsonResult = cachedResult.JsonResult
            };

            events.Add(new ToolResultEvent
            {
                CallId = callId,
                ToolName = toolName,
                Success = replayToolResult.Success,
                Output = replayToolResult.JsonResult,
                Error = replayToolResult.Error,
                IsReplayed = true
            });

            return new ToolDispatchResult
            {
                ToolResult = replayToolResult,
                PolicyHash = decision.PolicyHash,
                Events = events
            };
        }

        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        ToolResult toolResult;

        try
        {
            var output = await dispatchableTool.InvokeAsync(input);
            sw.Stop();

            toolResult = new ToolResult { CallId = callId, Success = true, JsonResult = output };

            events.Add(new ToolExecutedEvent
            {
                CallId = callId,
                ToolName = toolName,
                Input = input,
                DurationMs = sw.Elapsed.TotalMilliseconds
            });
        }
        catch (Exception ex)
        {
            sw.Stop();
            toolResult = new ToolResult { CallId = callId, Success = false, Error = ex.Message, JsonResult = string.Empty };
        }

        events.Add(new ToolResultEvent
        {
            CallId = callId,
            ToolName = toolName,
            Success = toolResult.Success,
            Output = toolResult.JsonResult,
            Error = toolResult.Error
        });

        return new ToolDispatchResult
        {
            ToolResult = toolResult,
            PolicyHash = decision.PolicyHash,
            Events = events
        };
    }
}
