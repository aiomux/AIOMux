using AIOMux.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text;

namespace AIOMux.Core;

/// <summary>
/// Manages agent registration, external package loading, and retrieval.
/// </summary>
public class AgentManager : IAgentManager
{
    private readonly List<IAgent> _agents = [];
    private readonly List<IAgent> _loadedExternalAgents = [];
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
            sb.AppendLine($"- {agent.Name}");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Gets available agents with their descriptions.
    /// </summary>
    /// <returns>Collection of agents with name and description.</returns>
    public IEnumerable<(string Name, string Description)> GetAvailableAgents()
    {
        return _agents.Select(a => (a.Name, a.Description));
    }

    /// <summary>
    /// Loads agents from a single agent package file asynchronously.
    /// </summary>
    /// <param name="assemblyPath">Path to the agent package file.</param>
    /// <param name="llmClientResolver">Named LLM client resolver. Each agent's preferred profile is
    /// resolved via <see cref="AgentMetadata.PreferredLlmProfile"/>, falling back to "default".</param>
    /// <param name="configuration">Optional configuration for the loaded agent(s).</param>
    /// <returns>True if at least one agent was loaded successfully.</returns>
    public async Task<bool> LoadAgentsFromAssemblyAsync(string assemblyPath, ILLMClientResolver? llmClientResolver = null, Dictionary<string, object>? configuration = null)
    {
        try
        {
            _logger?.LogInformation("Attempting to load agent assembly from: {AssemblyPath}", assemblyPath);

            if (!File.Exists(assemblyPath))
            {
                _logger?.LogError("Agent assembly not found: {AssemblyPath}", assemblyPath);
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
                if (Activator.CreateInstance(agentType) is not IAgent prototype)
                {
                    _logger?.LogError("Failed to create instance of agent type: {AgentType}", agentType.Name);
                    continue;
                }

                ValidateRequiredLlmProfiles(prototype.Metadata, llmClientResolver, _logger);

                try
                {
                    var factoryContext = new AgentFactoryContext
                    {
                        LlmResolver = llmClientResolver,
                        Configuration = configuration ?? []
                    };

                    var agent = prototype.CreateAgent(factoryContext);
                    var initialized = await agent.InitializeAsync(factoryContext.Configuration);
                    if (!initialized)
                    {
                        _logger?.LogError("Agent initialization failed: {AgentType}", agentType.Name);
                        continue;
                    }

                    Register(agent);
                    _loadedExternalAgents.Add(agent);
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
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading agent assembly: {AssemblyPath}", assemblyPath);
            return false;
        }
    }

    /// <summary>
    /// Loads agents from package files in a directory asynchronously.
    /// </summary>
    /// <param name="directoryPath">Directory containing agent package files.</param>
    /// <param name="llmClientResolver">Named LLM client resolver passed through to each loaded agent.</param>
    /// <param name="configuration">Optional configuration for loaded agents.</param>
    /// <returns>Number of package files with at least one successfully loaded agent.</returns>
    public async Task<int> LoadAgentsFromDirectoryAsync(string directoryPath, ILLMClientResolver? llmClientResolver = null, Dictionary<string, object>? configuration = null)
    {
        if (!Directory.Exists(directoryPath))
        {
            _logger?.LogWarning("Agent assembly directory does not exist: {DirectoryPath}", directoryPath);
            return 0;
        }

        var assemblyFiles = Directory.GetFiles(directoryPath, "*.dll", SearchOption.TopDirectoryOnly);
        var loadedCount = 0;

        foreach (var assemblyFile in assemblyFiles)
        {
            if (await LoadAgentsFromAssemblyAsync(assemblyFile, llmClientResolver, configuration))
                loadedCount++;
        }

        _logger?.LogInformation("Loaded {LoadedCount} agent assembly(ies) from directory: {DirectoryPath}",
            loadedCount, directoryPath);

        return loadedCount;
    }

    /// <summary>
    /// Unloads all externally loaded agents and cleans up resources.
    /// </summary>
    public async Task UnloadExternalAgentsAsync()
    {
        foreach (var agent in _loadedExternalAgents)
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

        foreach (var agent in _loadedExternalAgents)
            _agents.Remove(agent);

        _loadedExternalAgents.Clear();
        _logger?.LogInformation("All externally loaded agents have been unloaded");
    }

    /// <summary>
    /// Validates that all required LLM profiles declared by an agent are available in the resolver.
    /// When <paramref name="resolver"/> is null and profiles are required, logs a warning but does not fail.
    /// When <paramref name="resolver"/> is non-null, throws for any missing profile.
    /// This ensures SolutionRunner-loaded agents (which always pass a non-null resolver) hard-fail on missing profiles,
    /// while direct AgentManager usage without a resolver only warns.
    /// </summary>
    /// <param name="metadata">Agent metadata containing the required profiles list.</param>
    /// <param name="resolver">LLM client resolver to validate against.</param>
    /// <param name="logger">Optional logger for warning messages.</param>
    /// <exception cref="InvalidOperationException">Thrown when resolver is non-null and a required profile is missing.</exception>
    internal static void ValidateRequiredLlmProfiles(AgentMetadata metadata, ILLMClientResolver? resolver, ILogger? logger)
    {
        if (metadata.RequiredLlmProfiles.Count == 0)
            return;

        if (resolver == null)
        {
            logger?.LogWarning(
                "Agent '{AgentName}' declares required LLM profiles {Profiles} but no resolver is available. Profiles cannot be validated.",
                metadata.Name,
                string.Join(", ", metadata.RequiredLlmProfiles));
            return;
        }

        foreach (var profileName in metadata.RequiredLlmProfiles)
        {
            if (resolver.TryGet(profileName) == null)
            {
                throw new InvalidOperationException(
                    $"Agent '{metadata.Name}' requires LLM profile '{profileName}' which was not found in the resolver.");
            }
        }
    }
}