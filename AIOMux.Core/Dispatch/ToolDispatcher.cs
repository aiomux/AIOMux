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
///   2. If replay cache hit: emit <see cref="ToolResultEvent"/> and return early.
///   3. Resolve the tool; synthesize an unrecognized analysis when the tool is absent.
///   4. Call <c>ITool.Analyze</c> to classify requested operations.
///   5. Call <see cref="IPolicyEngine.Evaluate"/>; emit <see cref="PolicyEvaluatedEvent"/>.
///   6. If denied: return with <see cref="ToolDispatchResult.PolicyDenied"/> set.
///   7. Call <c>ITool.ExecuteAsync</c>; emit <see cref="ToolExecutedEvent"/>.
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

        // 1. Proposed
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

        // 2. Replay path: skip analysis and policy, return cached result directly.
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

            return new ToolDispatchResult { ToolResult = replayToolResult, Events = events };
        }

        // 3. Resolve tool. Missing tools produce a synthetic unrecognized analysis so the
        //    policy engine can emit a coherent denial rather than a bare exception.
        _tools.TryGetValue(toolName, out var tool);

        // 4. Analyze
        var analysis = tool != null
            ? tool.Analyze(input)
            : ToolExecutionAnalysis.Unrecognized($"Tool not found: {toolName}");

        // 5. Policy evaluation
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

        // 6. Denied path
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

        // 7. Execute
        ct.ThrowIfCancellationRequested();

        var sw = Stopwatch.StartNew();
        ToolResult toolResult;

        try
        {
            var output = await tool!.ExecuteAsync(input);
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

        // 8. Result
        events.Add(new ToolResultEvent
        {
            CallId = callId,
            ToolName = toolName,
            Success = toolResult.Success,
            Output = toolResult.JsonResult,
            Error = toolResult.Error
        });

        return new ToolDispatchResult { ToolResult = toolResult, Events = events };
    }
}
