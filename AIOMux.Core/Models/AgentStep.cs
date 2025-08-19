namespace AIOMux.Core.Models;

/// <summary>
/// Represents a single step in an agent chain.
/// </summary>
public class AgentStep
{
    /// <summary>
    /// Name of the agent to execute for this step.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Input source for this step. Can be a variable name from a previous step
    /// or empty to use the default context input.
    /// </summary>
    public string? InputFrom { get; set; }

    /// <summary>
    /// Optional variable name to store this step's output.
    /// If null, the agent name will be used.
    /// This can be referenced by a later step's InputFrom property.
    /// </summary>
    public string? OutputTo { get; set; }
}