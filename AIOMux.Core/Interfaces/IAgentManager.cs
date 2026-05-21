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
    /// Loads agents from the specified agent package path.
    /// When a non-null <paramref name="llmClientResolver"/> is provided, validates that all profiles
    /// listed in <see cref="AgentMetadata.RequiredLlmProfiles"/> exist; throws <see cref="InvalidOperationException"/>
    /// if any are missing.
    /// </summary>
    /// <param name="assemblyPath">Path to the agent package file.</param>
    /// <param name="llmClientResolver">Named LLM client resolver available to agents.</param>
    /// <param name="configuration">Optional configuration for loaded agents.</param>
    /// <returns>True if at least one agent was loaded successfully.</returns>
    Task<bool> LoadAgentsFromAssemblyAsync(string assemblyPath, ILLMClientResolver? llmClientResolver = null, Dictionary<string, object>? configuration = null);

    /// <summary>
    /// Loads agents from all matching agent package files in the specified directory.
    /// When a non-null <paramref name="llmClientResolver"/> is provided, validates that all profiles
    /// listed in <see cref="AgentMetadata.RequiredLlmProfiles"/> exist; throws <see cref="InvalidOperationException"/>
    /// if any are missing.
    /// </summary>
    /// <param name="directoryPath">Directory containing agent package files.</param>
    /// <param name="llmClientResolver">Named LLM client resolver available to agents.</param>
    /// <param name="configuration">Optional configuration for loaded agents.</param>
    /// <returns>Number of package files that loaded at least one agent.</returns>
    Task<int> LoadAgentsFromDirectoryAsync(string directoryPath, ILLMClientResolver? llmClientResolver = null, Dictionary<string, object>? configuration = null);

    /// <summary>
    /// Unloads all externally loaded agents and cleans up resources.
    /// </summary>
    Task UnloadExternalAgentsAsync();
}