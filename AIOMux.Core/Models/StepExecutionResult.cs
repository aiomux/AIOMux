namespace AIOMux.Core.Models;

/// <summary>
/// Result of executing a single step within a plan.
/// </summary>
public sealed record StepExecutionResult
{
    /// <summary>Whether the step completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Primary text output produced by the step.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>Named outputs produced by the step, keyed by output name.</summary>
    public Dictionary<string, object?> Outputs { get; init; } = new();

    /// <summary>Error message when the step fails.</summary>
    public string? Error { get; init; }
}
