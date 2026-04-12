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

    /// <summary>
    /// Initializes a new instance of <see cref="AgentManager"/>.
    /// </summary>
    /// <param name="loggerFactory">Optional logger factory used to create component loggers.</param>
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
            // Skip the planner agent because it drives plan generation, not plan steps.
            if (agent.Name.Contains("Planner", StringComparison.OrdinalIgnoreCase))
                continue;

            sb.AppendLine($"- {agent.Name}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Get available agents with their descriptions for use in planning.
    /// </summary>
    /// <returns>Collection of agents with name and description</returns>
    public IEnumerable<(string Name, string Description)> GetAvailableAgents()
    {
        return _agents
            .Where(a => !a.Name.Contains("Planner", StringComparison.OrdinalIgnoreCase))
            .Select(a => (a.Name, a.Description));
    }

    /// <summary>
    /// Loads a single plugin asynchronously.
    /// </summary>
    /// <param name="assemblyPath">Path to the plugin assembly.</param>
    /// <param name="llmProfiles">Named LLM client profiles. Each plugin's preferred profile is
    /// resolved via <see cref="AgentMetadata.PreferredLlmProfile"/>, falling back to "default".</param>
    /// <param name="configuration">Optional configuration for the plugin.</param>
    /// <returns>True if the plugin was loaded successfully.</returns>
    public async Task<bool> LoadPluginAsync(string assemblyPath, Dictionary<string, ILLMClient>? llmProfiles = null, Dictionary<string, object>? configuration = null)
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
                return false;

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

                    var initialized = await plugin.InitializeAsync(configuration);
                    if (!initialized)
                    {
                        _logger?.LogError("Plugin initialization failed: {PluginType}", pluginType.Name);
                        continue;
                    }

                    var llmClient = ResolveProfileClient(llmProfiles, plugin.Metadata.PreferredLlmProfile);

                    var agent = plugin.CreateAgent(llmClient, configuration);

                    if (!ValidateLlmConstraints(plugin.Metadata, llmClient))
                    {
                        _logger?.LogError(
                            "Plugin '{AgentName}' requires provider='{Provider}' model='{Model}' but the supplied LLM client does not satisfy these constraints. Load aborted.",
                            plugin.Metadata.Name,
                            plugin.Metadata.RequiredLlmProvider ?? "(any)",
                            plugin.Metadata.RequiredLlmModel ?? "(any)");
                        continue;
                    }

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
    /// <param name="pluginDirectory">Directory containing plugin assemblies.</param>
    /// <param name="llmProfiles">Named LLM client profiles passed through to each plugin.</param>
    /// <param name="configuration">Optional configuration for plugins.</param>
    /// <returns>Number of plugins successfully loaded.</returns>
    public async Task<int> LoadPluginsFromDirectoryAsync(string pluginDirectory, Dictionary<string, ILLMClient>? llmProfiles = null, Dictionary<string, object>? configuration = null)
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
            if (await LoadPluginAsync(pluginFile, llmProfiles, configuration))
                loadedCount++;
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

    /// <summary>
    /// Validates that the provided LLM client satisfies the compatibility constraints
    /// declared in an agent plugin's metadata.
    /// </summary>
    /// <param name="metadata">The plugin metadata containing optional constraint fields.</param>
    /// <param name="llmClient">The LLM client that will be injected into the agent.</param>
    /// <returns>
    /// True when no constraints are declared or all constraints are satisfied;
    /// false when constraints are declared but cannot be verified against the client.
    /// </returns>
    private static bool ValidateLlmConstraints(AgentMetadata metadata, ILLMClient? llmClient)
    {
        bool hasProviderConstraint = !string.IsNullOrEmpty(metadata.RequiredLlmProvider);
        bool hasModelConstraint = !string.IsNullOrEmpty(metadata.RequiredLlmModel);

        if (!hasProviderConstraint && !hasModelConstraint)
            return true;

        // Constraints are declared but no client was provided.
        if (llmClient == null)
            return false;

        if (hasProviderConstraint &&
            !llmClient.Provider.Equals(metadata.RequiredLlmProvider, StringComparison.OrdinalIgnoreCase))
            return false;

        if (hasModelConstraint && !MatchesModelPattern(metadata.RequiredLlmModel!, llmClient.Model))
            return false;

        return true;
    }

    /// <summary>
    /// Matches a model name against a required pattern.
    /// A trailing <c>*</c> acts as a prefix wildcard; otherwise an exact case-insensitive match is required.
    /// </summary>
    private static bool MatchesModelPattern(string required, string actual)
    {
        return required.EndsWith('*')
            ? actual.StartsWith(required[..^1], StringComparison.OrdinalIgnoreCase)
            : actual.Equals(required, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolves an LLM client from a named profile map.
    /// Uses <paramref name="preferredProfile"/> first, then falls back to "default".
    /// Returns null when no profiles are available.
    /// </summary>
    private static ILLMClient? ResolveProfileClient(Dictionary<string, ILLMClient>? profiles, string? preferredProfile)
    {
        if (profiles == null || profiles.Count == 0)
            return null;

        var key = preferredProfile ?? "default";

        if (profiles.TryGetValue(key, out var client))
            return client;

        profiles.TryGetValue("default", out var fallback);
        return fallback;
    }
}