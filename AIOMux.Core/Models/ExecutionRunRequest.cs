namespace AIOMux.Core.Models;

/// <summary>
/// Request to run an execution plan via the runtime.
/// </summary>
public sealed record ExecutionRunRequest
{
    /// <summary>The plan to execute.</summary>
    public ExecutionPlan Plan { get; init; } = default!;

    /// <summary>The context carrying run identity, inputs, state, records, and runtime dependencies.</summary>
    public ExecutionContext Context { get; init; } = default!;
}
