namespace AIOMux.Core.Models;

/// <summary>
/// Single native execution trace model capturing the result of a step execution.
/// Serves as the authoritative record for all step executions, denials, failures, and replay/fork reconstruction.
/// </summary>
public sealed class ExecutionRecord
{
    /// <summary>
    /// Identifier for the run.
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Identifier for the step.
    /// </summary>
    public string StepId { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based index of the step in the execution plan.
    /// </summary>
    public int StepIndex { get; set; }

    /// <summary>
    /// Step type (e.g., "tool", "agent").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Step target (tool name or agent name).
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Input provided to the step.
    /// </summary>
    public object? Input { get; set; }

    /// <summary>
    /// Output produced by the step.
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// Primary state key receiving Output when the step succeeds.
    /// </summary>
    public string? OutputKey { get; set; }

    /// <summary>
    /// All state mutations written by this step when it succeeds.
    /// Key is the state key and value is the written value.
    /// </summary>
    public Dictionary<string, object?> StateChanges { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the step completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error captured for a failed or denied step.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Reason for policy denial, if applicable. Null if step was not policy-denied.
    /// </summary>
    public string? PolicyDenyReason { get; set; }

    /// <summary>
    /// Hash of the policy that evaluated this step, for audit trail.
    /// </summary>
    public string? PolicyHash { get; set; }

    /// <summary>
    /// When the record was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Elapsed execution time in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }
}
