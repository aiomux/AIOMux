using AIOMux.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text;

namespace AIOMux.Core;

/// <summary>
/// Manages agent registration, plugin loading, and retrieval.
/// </summary>
public class AgentManager : IAgentManager
{
    private readonly List<IAgent> _agents = [];
    private readonly List<IAgentPlugin> _loadedPlugins = [];
    private readonly ILogger<AgentManager>? _logger;
    public ILoggerFactory? LoggerFactory { get; }

    // Accept optional ILoggerFactory
    public AgentManager(ILoggerFactory? loggerFactory = null)
    {
        LoggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<AgentManager>();
    }

    /// <summary>
    /// Registers an agent instance.
    /// </summary>
    public void Register(IAgent agent) => _agents.Add(agent);

    /// <summary>
    /// Gets an agent by name.
    /// </summary>
    /// <param name="name">The name of the agent to retrieve.</param>
    /// <returns>The agent with the specified name, or null if not found.</returns>
    public IAgent? GetByName(string name) =>
        _agents.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Get a list of all registered agents.
    /// </summary>
    /// <returns>List of all registered agents</returns>
    public IReadOnlyList<IAgent> GetAllAgents() => _agents.AsReadOnly();

    /// <summary>
    /// Get a formatted string listing all available agents with their names.
    /// </summary>
    /// <returns>Formatted string of agent names</returns>
    public string GetFormattedAgentList()
    {
        var sb = new StringBuilder();
        foreach (var agent in _agents)
        {
            // Skip the planner agent itself as it's not meant to be used in chains
            if (agent.Name.Contains("Planner", StringComparison.OrdinalIgnoreCase))
                continue;

            sb.AppendLine($"- {agent.Name}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Creates and registers a new agent chain with the specified name.
    /// </summary>
    /// <param name="name">Unique name for the chain</param>
    /// <returns>The newly created agent chain</returns>
    public AgentChain CreateChain(string name)
    {
        var chain = new AgentChain(name);
        Register(chain);
        return chain;
    }

    /// <summary>
    /// Creates a new agent chain that executes the specified agents in sequence.
    /// </summary>
    /// <param name="name">Name of the chain</param>
    /// <param name="agentNames">Names of agents to include in the chain</param>
    /// <returns>The created agent chain, or null if any agent wasn't found</returns>
    public AgentChain? CreateChainFromExisting(string name, IEnumerable<string> agentNames)
    {
        var chain = new AgentChain(name);

        foreach (var agentName in agentNames)
        {
            var agent = GetByName(agentName);
            if (agent == null)
                return null;

            chain.AddAgent(agent);
        }

        Register(chain);
        return chain;
    }

    /// <summary>
    /// Get available agents with their descriptions for use in planning.
    /// </summary>
    /// <returns>Collection of agents with name and description</returns>
    public IEnumerable<(string Name, string Description)> GetAvailableAgents()
    {
        return _agents
            .Where(a => !a.Name.Contains("Planner", StringComparison.OrdinalIgnoreCase))
            .Select(a =>
            {
                string description = "No description available";
                if (a is IAgentExtended extended)
                {
                    description = extended.Capabilities?.Description ?? description;
                }
                return (a.Name, description);
            });
    }

    /// <summary>
    /// Loads a single plugin asynchronously.
    /// </summary>
    /// <param name="assemblyPath">Path to the plugin assembly</param>
    /// <param name="llmClient">Optional LLM client to provide to the plugin</param>
    /// <param name="configuration">Optional configuration for the plugin</param>
    /// <returns>True if the plugin was loaded successfully</returns>
    public async Task<bool> LoadPluginAsync(string assemblyPath, ILLMClient? llmClient = null, Dictionary<string, object>? configuration = null)
    {
        try
        {
            _logger?.LogInformation("Attempting to load plugin from: {AssemblyPath}", assemblyPath);

            if (!File.Exists(assemblyPath))
            {
                _logger?.LogError("Plugin assembly not found: {AssemblyPath}", assemblyPath);
                return false;
            }

            var assembly = Assembly.LoadFrom(assemblyPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IAgentPlugin).IsAssignableFrom(t))
                .ToArray();

            if (pluginTypes.Length == 0)
            {
                _logger?.LogWarning("No IAgentPlugin implementations found in: {AssemblyPath}", assemblyPath);
                return false;
            }

            foreach (var pluginType in pluginTypes)
            {
                try
                {
                    var plugin = Activator.CreateInstance(pluginType) as IAgentPlugin;
                    if (plugin == null)
                    {
                        _logger?.LogError("Failed to create instance of plugin type: {PluginType}", pluginType.Name);
                        continue;
                    }

                    // Initialize the plugin
                    var initialized = await plugin.InitializeAsync(configuration);
                    if (!initialized)
                    {
                        _logger?.LogError("Plugin initialization failed: {PluginType}", pluginType.Name);
                        continue;
                    }

                    // Create and register the agent
                    var agent = plugin.CreateAgent(llmClient, configuration);
                    Register(agent);
                    _loadedPlugins.Add(plugin);

                    _logger?.LogInformation("Successfully loaded plugin: {AgentName} from {PluginType}",
                        agent.Name, pluginType.Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error loading plugin type: {PluginType}", pluginType.Name);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading plugin assembly: {AssemblyPath}", assemblyPath);
            return false;
        }
    }

    /// <summary>
    /// Loads plugins from a directory asynchronously.
    /// </summary>
    /// <param name="pluginDirectory">Directory containing plugin assemblies</param>
    /// <param name="llmClient">Optional LLM client to provide to plugins</param>
    /// <param name="configuration">Optional configuration for plugins</param>
    /// <returns>Number of plugins successfully loaded</returns>
    public async Task<int> LoadPluginsFromDirectoryAsync(string pluginDirectory, ILLMClient? llmClient = null, Dictionary<string, object>? configuration = null)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            _logger?.LogWarning("Plugin directory does not exist: {PluginDirectory}", pluginDirectory);
            return 0;
        }

        var pluginFiles = Directory.GetFiles(pluginDirectory, "AIOMux.Plugin.*.dll", SearchOption.TopDirectoryOnly);
        var loadedCount = 0;

        foreach (var pluginFile in pluginFiles)
        {
            if (await LoadPluginAsync(pluginFile, llmClient, configuration))
            {
                loadedCount++;
            }
        }

        _logger?.LogInformation("Loaded {LoadedCount} plugins from directory: {PluginDirectory}",
            loadedCount, pluginDirectory);

        return loadedCount;
    }

    /// <summary>
    /// Unloads all plugins and cleans up resources.
    /// </summary>
    public async Task UnloadAllPluginsAsync()
    {
        foreach (var plugin in _loadedPlugins)
        {
            try
            {
                await plugin.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing plugin: {PluginType}", plugin.GetType().Name);
            }
        }

        // Remove plugin-based agents from the registry
        var pluginAgents = _agents.Where(a => _loadedPlugins.Any(p => p.Metadata.Name == a.Name)).ToList();
        foreach (var agent in pluginAgents)
        {
            _agents.Remove(agent);
        }

        _loadedPlugins.Clear();
        _logger?.LogInformation("All plugins have been unloaded");
    }
}