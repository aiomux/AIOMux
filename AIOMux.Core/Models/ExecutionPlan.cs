namespace AIOMux.Core.Models;

/// <summary>
/// Represents a canonical execution plan.
/// </summary>
public sealed class ExecutionPlan
{
    /// <summary>
    /// Name of the plan.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the plan.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Origin of the plan.
    /// </summary>
    public PlanSource Source { get; set; } = PlanSource.Static;

    /// <summary>
    /// Steps to execute.
    /// </summary>
    public IReadOnlyList<ExecutionStep> Steps { get; set; } = [];

    /// <summary>
    /// Additional metadata associated with the plan.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new();
}
