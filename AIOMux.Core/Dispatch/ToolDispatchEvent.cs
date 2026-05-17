using AIOMux.Core.Policy;

namespace AIOMux.Core.Dispatch;

/// <summary>
/// Base type for all events produced during a single tool dispatch cycle.
/// Events are emitted in declaration order: Proposed, PolicyEvaluated, Executed, Result.
/// On a policy denial the sequence stops after <see cref="PolicyEvaluatedEvent"/>.
/// On a replayed invocation only <see cref="ToolProposedEvent"/> and <see cref="ToolResultEvent"/> are emitted.
/// </summary>
public abstract class ToolDispatchEvent
{
    /// <summary>Deterministic identifier for this specific tool invocation.</summary>
    public string CallId { get; init; } = string.Empty;

    /// <summary>Name of the tool being dispatched.</summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>UTC timestamp at the moment this event was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Emitted immediately when an invocation is received, before analysis or policy.
/// Always the first event in the sequence.
/// </summary>
public sealed class ToolProposedEvent : ToolDispatchEvent
{
    /// <summary>Raw input string passed to the tool.</summary>
    public string Input { get; init; } = string.Empty;

    /// <summary>True when this invocation will be served from the replay cache.</summary>
    public bool IsReplayed { get; init; }
}

/// <summary>
/// Emitted after <c>ITool.Analyze</c> and <see cref="IPolicyEngine.Evaluate"/> complete.
/// Only produced for live (non-replayed) invocations.
/// When <see cref="Allowed"/> is false no further events are emitted.
/// </summary>
public sealed class PolicyEvaluatedEvent : ToolDispatchEvent
{
    /// <summary>
    /// Operations the tool reported it would perform for this specific input.
    /// Empty when <see cref="IsRecognized"/> is false.
    /// </summary>
    public IReadOnlyCollection<ToolOperation> RequestedOperations { get; init; } = [];

    /// <summary>
    /// Targets the tool reported it would access for this specific input.
    /// </summary>
    public IReadOnlyCollection<Models.ToolTarget> Targets { get; init; } = [];

    /// <summary>False when <c>ITool.Analyze</c> could not classify the input.</summary>
    public bool IsRecognized { get; init; }

    /// <summary>True when the policy engine permitted the invocation.</summary>
    public bool Allowed { get; init; }

    /// <summary>Denial reason supplied by the policy engine. Null when allowed.</summary>
    public string? Reason { get; init; }

    /// <summary>Stable identifier of the policy configuration that made this decision.</summary>
    public string PolicyHash { get; init; } = string.Empty;
}

/// <summary>
/// Emitted immediately after <c>ITool.ExecuteAsync</c> returns (success or exception).
/// Only produced when the invocation was live and policy allowed execution.
/// </summary>
public sealed class ToolExecutedEvent : ToolDispatchEvent
{
    /// <summary>Input forwarded to <c>ExecuteAsync</c>.</summary>
    public string Input { get; init; } = string.Empty;

    /// <summary>Wall-clock duration of the <c>ExecuteAsync</c> call in milliseconds.</summary>
    public double DurationMs { get; init; }
}

/// <summary>
/// Emitted as the final event of every dispatch cycle, regardless of outcome.
/// Produced for both live and replayed invocations.
/// </summary>
public sealed class ToolResultEvent : ToolDispatchEvent
{
    /// <summary>True when the tool produced a result without throwing.</summary>
    public bool Success { get; init; }

    /// <summary>Serialized output from the tool. Null on failure.</summary>
    public string? Output { get; init; }

    /// <summary>Error message when the execution failed. Null on success.</summary>
    public string? Error { get; init; }

    /// <summary>True when the result was served from the replay cache.</summary>
    public bool IsReplayed { get; init; }
}
