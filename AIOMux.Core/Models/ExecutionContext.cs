namespace AIOMux.Core.Models;

/// <summary>
/// Run-state container for a single execution.
/// Holds entry inputs, mutable state, and canonical execution records.
/// </summary>
public sealed class ExecutionContext
{
    /// <summary>Unique identifier for this run.</summary>
    public string RunId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Named inputs provided to the plan before execution begins.
    /// The primary user input is stored under the key <c>"input"</c>.
    /// </summary>
    public Dictionary<string, object?> Inputs { get; set; } = new();

    /// <summary>Working directory at execution time.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Mutable key/value state written by the runtime during execution.
    /// <c>State["input"]</c> holds the resolved step input.
    /// Step outputs are stored by <see cref="ExecutionStep.OutputKey"/>.
    /// </summary>
    public Dictionary<string, object?> State { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runtime dependencies and execution settings for this run.
    /// </summary>
    public ExecutionRuntimeServices Services { get; set; } = new();

    /// <summary>Canonical execution records captured during this run.</summary>
    public List<ExecutionRecord> Records { get; set; } = [];

    /// <summary>
    /// Returns the resolved step input: <c>State["input"]</c> when available,
    /// falling back to entry input <c>Inputs["input"]</c>.
    /// </summary>
    public string GetInput() =>
        (State.TryGetValue("input", out var s) ? s?.ToString() : null)
        ?? (Inputs.TryGetValue("input", out var i) ? i?.ToString() : null)
        ?? string.Empty;
}
