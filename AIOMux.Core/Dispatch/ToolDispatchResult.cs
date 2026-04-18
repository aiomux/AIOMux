using AIOMux.Core.Models;

namespace AIOMux.Core.Dispatch;

/// <summary>
/// The complete outcome of a <see cref="ToolDispatcher.InvokeAsync"/> call.
/// Contains the final tool result, policy denial details when applicable,
/// and the ordered sequence of events produced during the dispatch cycle.
/// </summary>
public sealed class ToolDispatchResult
{
    /// <summary>
    /// The raw tool result. On a policy denial <see cref="ToolResult.Success"/> is false
    /// and <see cref="ToolResult.Error"/> holds the denial reason.
    /// </summary>
    public ToolResult ToolResult { get; init; } = new();

    /// <summary>True when the invocation was blocked by the policy engine.</summary>
    public bool PolicyDenied { get; init; }

    /// <summary>Human-readable denial reason. Null when <see cref="PolicyDenied"/> is false.</summary>
    public string? PolicyDenyReason { get; init; }

    /// <summary>Policy hash from the evaluation decision. Empty when not applicable.</summary>
    public string PolicyHash { get; init; } = string.Empty;

    /// <summary>
    /// Ordered events emitted during this dispatch cycle.
    /// Replay: [Proposed, Result].
    /// Denied: [Proposed, PolicyEvaluated].
    /// Allowed: [Proposed, PolicyEvaluated, Executed, Result].
    /// </summary>
    public IReadOnlyList<ToolDispatchEvent> Events { get; init; } = [];
}
