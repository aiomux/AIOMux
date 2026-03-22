namespace AIOMux.Core.Models;

/// <summary>
/// Holds state for a single execution run.
/// </summary>
public sealed class ExecutionContext
{
    /// <summary>
    /// Unique identifier for the run.
    /// </summary>
    public string RunId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Initial inputs for the run.
    /// </summary>
    public Dictionary<string, object?> Inputs { get; set; } = new();

    /// <summary>
    /// Mutable state produced during execution.
    /// </summary>
    public Dictionary<string, object?> State { get; set; } = new();

    /// <summary>
    /// Execution records captured for the run.
    /// </summary>
    public List<ExecutionRecord> Records { get; set; } = new();
}
