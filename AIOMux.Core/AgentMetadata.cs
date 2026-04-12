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

    /// <summary>
    /// Required LLM provider identifier (for example: "openai" or "ollama").
    /// When set, the runtime enforces that the resolved LLM client uses this provider.
    /// Null means no restriction.
    /// </summary>
    public string? RequiredLlmProvider { get; set; }

    /// <summary>
    /// Required LLM model name or wildcard pattern (for example: "gpt-4o*").
    /// A trailing wildcard matches any model with the given prefix.
    /// When set, the runtime enforces that the resolved LLM client targets a matching model.
    /// Null means no restriction.
    /// </summary>
    public string? RequiredLlmModel { get; set; }

    /// <summary>
    /// Preferred named LLM profile key from the solution's <c>llmProfiles</c> map.
    /// When set, the runtime selects this profile by default for this agent.
    /// Falls back to <c>"default"</c> if the key is absent from the solution.
    /// </summary>
    public string? PreferredLlmProfile { get; set; }
}