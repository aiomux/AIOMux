namespace AIOMux.Core.Models;

/// <summary>
/// Represents a single execution step within a plan.
/// </summary>
public sealed class ExecutionStep
{
    /// <summary>
    /// Identifier for the step.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Step type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Target agent or tool for the step.
    /// </summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// Input values for the step.
    /// </summary>
    public Dictionary<string, object?> Inputs { get; set; } = new();

    /// <summary>
    /// Bindings from context state into step inputs.
    /// </summary>
    public Dictionary<string, string> Bindings { get; set; } = new();

    /// <summary>
    /// Optional key used to store the step output.
    /// </summary>
    public string? OutputKey { get; set; }
}
