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
    /// Creates and registers a new agent chain with the specified name.
    /// </summary>
    /// <param name="name">Unique name for the chain</param>
    /// <returns>The newly created agent chain</returns>
    AgentChain CreateChain(string name);

    /// <summary>
    /// Creates a new agent chain that executes the specified agents in sequence.
    /// </summary>
    /// <param name="name">Name of the chain</param>
    /// <param name="agentNames">Names of agents to include in the chain</param>
    /// <returns>The created agent chain, or null if any agent wasn't found</returns>
    AgentChain? CreateChainFromExisting(string name, IEnumerable<string> agentNames);

    /// <summary>
    /// Get available agents with their descriptions for use in planning.
    /// </summary>
    /// <returns>Collection of agents with name and description</returns>
    IEnumerable<(string Name, string Description)> GetAvailableAgents();

    /// <summary>
    /// Loads an agent plugin from the specified assembly path.
    /// </summary>
    /// <param name="assemblyPath">Path to the plugin assembly</param>
    /// <param name="llmClient">Optional LLM client to provide to the plugin</param>
    /// <param name="configuration">Optional configuration for the plugin</param>
    /// <returns>True if the plugin was loaded successfully</returns>
    Task<bool> LoadPluginAsync(string assemblyPath, ILLMClient? llmClient = null, Dictionary<string, object>? configuration = null);

    /// <summary>
    /// Loads all plugins from the specified directory.
    /// </summary>
    /// <param name="pluginDirectory">Directory containing plugin assemblies</param>
    /// <param name="llmClient">Optional LLM client to provide to plugins</param>
    /// <param name="configuration">Optional configuration for plugins</param>
    /// <returns>Number of plugins successfully loaded</returns>
    Task<int> LoadPluginsFromDirectoryAsync(string pluginDirectory, ILLMClient? llmClient = null, Dictionary<string, object>? configuration = null);

    /// <summary>
    /// Unloads all plugins and cleans up resources.
    /// </summary>
    Task UnloadAllPluginsAsync();
}