namespace AIOMux.Core.Models;

/// <summary>
/// Result of running an agent or chain via the runtime facade.
/// </summary>
public sealed record AgentRuntimeResult
{
    /// <summary>
    /// Indicates whether the execution completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The output from the agent or chain execution.
    /// </summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// If a chain failed mid-execution, the index of the step that failed.
    /// </summary>
    public int? StepIndex { get; init; }

    /// <summary>
    /// If a chain failed mid-execution, the name of the agent that failed.
    /// </summary>
    public string? AgentName { get; init; }
}
