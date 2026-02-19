namespace AIOMux.Core.Models;

/// <summary>
/// Structured result of a chain execution, providing detailed information about success/failure.
/// </summary>
public record ChainRunResult(
    bool Success,
    string Output,
    string? Error = null,
    int? StepIndex = null,
    string? AgentName = null
);

