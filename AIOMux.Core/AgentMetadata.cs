namespace AIOMux.Core;

/// <summary>
/// Metadata describing an agent, including name, description, version, and supported features.
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
    /// When the agent was last updated.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the list of supported tasks.
    /// </summary>
    public List<string> SupportedTasks { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of supported languages.
    /// </summary>
    public List<string> SupportedLanguages { get; set; } = new();

    /// <summary>
    /// Gets or sets the input formats supported by the agent.
    /// </summary>
    public List<string> InputFormats { get; set; } = new();

    /// <summary>
    /// Gets or sets the output formats supported by the agent.
    /// </summary>
    public List<string> OutputFormats { get; set; } = new();
}