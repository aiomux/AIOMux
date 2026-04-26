namespace AIOMux.Core.Interfaces;

/// <summary>
/// Defines the contract for managing agent registration and retrieval.
/// </summary>
public interface IAgentManager
{
    /// <summary> Register a single agent instance. </summary>
    void Register(IAgent agent);

    /// <summary> Retrieve an agent by name, or null if missing. </summary>
    IAgent? GetByName(string name);

    /// <summary>
    /// Get a list of all registered agents.
    /// </summary>
    /// <returns>List of all registered agents</returns>
    IReadOnlyList<IAgent> GetAllAgents();

    /// <summary>
    /// Get a formatted string listing all available agents with their names.
    /// </summary>
    /// <returns>Formatted string of agent names</returns>
    string GetFormattedAgentList();

    /// <summary>
    /// Get available agents with their descriptions for use in planning.
    /// </summary>
    /// <returns>Collection of agents with name and description</returns>
    IEnumerable<(string Name, string Description)> GetAvailableAgents();

    /// <summary>
    /// Loads agents from the specified assembly path.
    /// Each agent's preferred LLM profile is resolved from <paramref name="llmProfiles"/>
    /// using <see cref="AgentMetadata.PreferredLlmProfile"/>, falling back to the "default" key.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <param name="llmProfiles">Named LLM client profiles available to agents.</param>
    /// <param name="configuration">Optional configuration for loaded agents.</param>
    /// <returns>True if at least one agent was loaded successfully.</returns>
    Task<bool> LoadAgentsFromAssemblyAsync(string assemblyPath, Dictionary<string, ILLMClient>? llmProfiles = null, Dictionary<string, object>? configuration = null);

    /// <summary>
    /// Loads agents from all matching assemblies in the specified directory.
    /// Each agent's preferred LLM profile is resolved from <paramref name="llmProfiles"/>
    /// using <see cref="AgentMetadata.PreferredLlmProfile"/>, falling back to the "default" key.
    /// </summary>
    /// <param name="directoryPath">Directory containing agent assemblies.</param>
    /// <param name="llmProfiles">Named LLM client profiles available to agents.</param>
    /// <param name="configuration">Optional configuration for loaded agents.</param>
    /// <returns>Number of assemblies that loaded at least one agent.</returns>
    Task<int> LoadAgentsFromDirectoryAsync(string directoryPath, Dictionary<string, ILLMClient>? llmProfiles = null, Dictionary<string, object>? configuration = null);

    /// <summary>
    /// Unloads all externally loaded agents and cleans up resources.
    /// </summary>
    Task UnloadExternalAgentsAsync();
}