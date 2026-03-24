namespace AIOMux.Core.Models;

/// <summary>
/// Captures the result of a single execution step.
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
    /// Step type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Step target.
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
    /// Whether the step completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error captured for a failed step.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// When the record was captured.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Elapsed execution time in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }
}
