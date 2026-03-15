namespace AIOMux.Core;

/// <summary>
/// Metadata describing an agent, including name, description, and version.
/// </summary>
public class AgentMetadata
{
    /// <summary>
    /// Gets or sets the name of the agent.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the agent.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the agent.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Path to the assembly containing the agent.
    /// </summary>
    public string? AssemblyPath { get; set; }

    /// <summary>
    /// Gets or sets the list of supported tasks.
    /// </summary>
    public List<string> SupportedTasks { get; set; } = new();
}