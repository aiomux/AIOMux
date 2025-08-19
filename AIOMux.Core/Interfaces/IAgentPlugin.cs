namespace AIOMux.Core.Interfaces;

/// <summary>
/// Interface that defines the contract for agent plugins that can be dynamically loaded.
/// </summary>
public interface IAgentPlugin
{
    /// <summary>
    /// Gets the metadata about this plugin.
    /// </summary>
    AgentMetadata Metadata { get; }

    /// <summary>
    /// Creates and returns an instance of the agent.
    /// </summary>
    /// <param name="llmClient">The LLM client to be used by the agent if needed</param>
    /// <param name="configuration">Optional configuration parameters for the agent</param>
    /// <returns>An instance of the agent</returns>
    IAgent CreateAgent(ILLMClient? llmClient = null, Dictionary<string, object>? configuration = null);

    /// <summary>
    /// Initializes the plugin with any required setup.
    /// </summary>
    /// <param name="configuration">Optional configuration parameters</param>
    /// <returns>True if initialization was successful</returns>
    Task<bool> InitializeAsync(Dictionary<string, object>? configuration = null);

    /// <summary>
    /// Cleans up any resources used by the plugin.
    /// </summary>
    /// <returns>Task representing the cleanup operation</returns>
    Task DisposeAsync();
}