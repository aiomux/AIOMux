namespace AIOMux.Core.Models;

/// <summary>
/// Result of running an execution plan via the runtime.
/// </summary>
public sealed record ExecutionResult
{
    /// <summary>Indicates whether the run completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Output produced by the final step of the plan.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>Error message when the run fails.</summary>
    public string? Error { get; init; }

    /// <summary>Zero-based index of the step that failed, if applicable.</summary>
    public int? StepIndex { get; init; }

    /// <summary>ID of the step that failed, if applicable.</summary>
    public string? StepId { get; init; }
}
