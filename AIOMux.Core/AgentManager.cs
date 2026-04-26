using AIOMux.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text;

namespace AIOMux.Core;

/// <summary>
/// Manages agent registration, extension loading, and retrieval.
/// </summary>
public class AgentManager : IAgentManager
{
    private readonly List<IAgent> _agents = [];
    private readonly List<IAgent> _loadedExtensionAgents = [];
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
    /// Loads a single extension assembly asynchronously.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly.</param>
    /// <param name="llmProfiles">Named LLM client profiles. Each agent's preferred profile is
    /// resolved via <see cref="AgentMetadata.PreferredLlmProfile"/>, falling back to "default".</param>
    /// <param name="configuration">Optional configuration for the loaded agent(s).</param>
    /// <returns>True if at least one agent was loaded successfully.</returns>
    public async Task<bool> LoadAgentsFromAssemblyAsync(string assemblyPath, Dictionary<string, ILLMClient>? llmProfiles = null, Dictionary<string, object>? configuration = null)
    {
        try
        {
            _logger?.LogInformation("Attempting to load extension assembly from: {AssemblyPath}", assemblyPath);

            if (!File.Exists(assemblyPath))
            {
                _logger?.LogError("Extension assembly not found: {AssemblyPath}", assemblyPath);
                return false;
            }

            var assembly = Assembly.LoadFrom(assemblyPath);
            var agentTypes = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IAgent).IsAssignableFrom(t))
                .ToArray();

            if (agentTypes.Length == 0)
                return false;

            var loadedAny = false;

            foreach (var agentType in agentTypes)
            {
                try
                {
                    if (Activator.CreateInstance(agentType) is not IAgent prototype)
                    {
                        _logger?.LogError("Failed to create instance of agent type: {AgentType}", agentType.Name);
                        continue;
                    }

                    var llmClient = ResolveProfileClient(llmProfiles, prototype.Metadata.PreferredLlmProfile);

                    if (!ValidateLlmConstraints(prototype.Metadata, llmClient))
                    {
                        _logger?.LogError(
                            "Agent '{AgentName}' requires provider='{Provider}' model='{Model}' but the supplied LLM client does not satisfy these constraints. Load aborted.",
                            prototype.Metadata.Name,
                            prototype.Metadata.RequiredLlmProvider ?? "(any)",
                            prototype.Metadata.RequiredLlmModel ?? "(any)");
                        continue;
                    }

                    var agent = prototype.CreateAgent(llmClient, configuration);
                    var initialized = await agent.InitializeAsync(configuration);
                    if (!initialized)
                    {
                        _logger?.LogError("Agent initialization failed: {AgentType}", agentType.Name);
                        continue;
                    }

                    Register(agent);
                    _loadedExtensionAgents.Add(agent);
                    loadedAny = true;

                    _logger?.LogInformation("Successfully loaded agent: {AgentName} from {AgentType}",
                        agent.Name, agentType.Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error loading agent type: {AgentType}", agentType.Name);
                }
            }

            return loadedAny;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading extension assembly: {AssemblyPath}", assemblyPath);
            return false;
        }
    }

    /// <summary>
    /// Loads extensions from a directory asynchronously.
    /// </summary>
    /// <param name="directoryPath">Directory containing extension assemblies.</param>
    /// <param name="llmProfiles">Named LLM client profiles passed through to each loaded agent.</param>
    /// <param name="configuration">Optional configuration for loaded agents.</param>
    /// <returns>Number of assemblies with at least one successfully loaded agent.</returns>
    public async Task<int> LoadAgentsFromDirectoryAsync(string directoryPath, Dictionary<string, ILLMClient>? llmProfiles = null, Dictionary<string, object>? configuration = null)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger?.LogWarning("Extension directory does not exist: {DirectoryPath}", directoryPath);
            return 0;
        }

        var assemblyFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly);
        var loadedCount = 0;

        foreach (var assemblyFile in assemblyFiles)
        {
            if (await LoadAgentsFromAssemblyAsync(assemblyFile, llmProfiles, configuration))
                loadedCount++;
        }

        _logger?.LogInformation("Loaded {LoadedCount} extension assembly(ies) from directory: {DirectoryPath}",
            loadedCount, directoryPath);

        return loadedCount;
    }

    /// <summary>
    /// Unloads all extension agents and cleans up resources.
    /// </summary>
    public async Task UnloadExternalAgentsAsync()
    {
        foreach (var agent in _loadedExtensionAgents)
        {
            try
            {
                await agent.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing agent: {AgentType}", agent.GetType().Name);
            }
        }

        foreach (var agent in _loadedExtensionAgents)
            _agents.Remove(agent);

        _loadedExtensionAgents.Clear();
        _logger?.LogInformation("All extension agents have been unloaded");
    }

    /// <summary>
    /// Validates that the provided LLM client satisfies the compatibility constraints
    /// declared in an agent metadata object.
    /// </summary>
    /// <param name="metadata">The agent metadata containing optional constraint fields.</param>
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