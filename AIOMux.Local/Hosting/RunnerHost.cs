using AIOMux.Clients;
using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Memory;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using AIOMux.Local.Config;
using AIOMux.Local.Security;
using AIOMux.Local.Skills;

namespace AIOMux.Local.Hosting;

/// <summary>
/// Hosts the orchestration runtime for local AIOMux execution.
/// Loads configuration, skills, and manages agent execution.
/// </summary>
public class RunnerHost
{
    private readonly AiomuxConfig _config;
    private readonly IAgentManager _agentManager;
    private readonly IAgentRuntime _runtime;
    private readonly SkillLoader _skillLoader;
    private readonly PermissionService _permissionService;
    private readonly IPolicyEngine _policyEngine;
    private readonly IMemoryStore _memoryStore;
    private readonly string _basePath;

    public RunnerHost(string? basePath = null)
    {
        _basePath = basePath ?? Directory.GetCurrentDirectory();
        _config = ConfigLoader.Load(_basePath);
        _permissionService = new PermissionService(_config.Permissions);
        _policyEngine = new ConfigPermissionPolicyEngine(_permissionService);
        _memoryStore = new JsonFileMemoryStore(Path.Combine(_basePath, "sandbox", "memory.json"));
        _skillLoader = new SkillLoader(Path.Combine(_basePath, _config.SkillsPath));
        _agentManager = new AgentManager();
        var plannerLlm = CreatePlannerClient();
        _agentManager.Register(new PlannerAgent(plannerLlm));
        var orchestrator = new AgentOrchestrator(_agentManager);
        _runtime = new AgentRuntime(_agentManager, orchestrator);
    }

    /// <summary>
    /// Initializes the runtime by loading skills and registering agents.
    /// </summary>
    public async Task InitializeAsync()
    {
        Console.WriteLine("Initializing AIOMux runtime...");

        // Load plugins/skills from the skills directory
        await LoadSkillsAsync();

        if (_agentManager.GetAllAgents().Count == 0)
        {
            Console.WriteLine("No agents loaded. Add skill plugins to the skills directory.");
        }

        var configured = _config.DefaultAgentName;
        var effective = ResolveDefaultAgentName();
        if (!configured.Equals(effective, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Configured default agent '{configured}' not found. Falling back to '{effective}'.");
        }

        PrintBanner();
    }

    /// <summary>
    /// Loads all available skills/plugins from the skills directory.
    /// </summary>
    private async Task LoadSkillsAsync()
    {
        var pluginPaths = _skillLoader.DiscoverPlugins().ToList();

        if (pluginPaths.Count == 0)
        {
            Console.WriteLine("No plugins found in skills directory (this is okay for MVP)");
            return;
        }

        Console.WriteLine($"Loading {pluginPaths.Count} plugin(s)...");

        int loadedCount = 0;
        foreach (var pluginPath in pluginPaths)
        {
            try
            {
                var success = await _agentManager.LoadPluginAsync(pluginPath);
                if (success)
                {
                    loadedCount++;
                    Console.WriteLine($"  Loaded: {Path.GetFileName(pluginPath)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Failed to load {Path.GetFileName(pluginPath)}: {ex.Message}");
            }
        }

        if (loadedCount > 0)
        {
            Console.WriteLine($"Loaded {loadedCount}/{pluginPaths.Count} plugin(s)");
        }
    }

    /// <summary>
    /// Prints the banner with runtime information.
    /// </summary>
    private void PrintBanner()
    {
        Console.WriteLine();
        Console.WriteLine("=========================================");
        Console.WriteLine("        AIOMux Local Runtime");
        Console.WriteLine("=========================================");
        Console.WriteLine();

        var agents = _agentManager.GetAllAgents();
        Console.WriteLine($"Loaded Agents: {agents.Count}");
        if (agents.Count > 0)
        {
            foreach (var agent in agents)
            {
                Console.WriteLine($"   - {agent.Name}");
            }
        }

        var skills = _skillLoader.GetSkillFolders().ToList();
        Console.WriteLine($"Skills: {skills.Count}");

        Console.WriteLine($"Model Provider: {_config.Model.Provider}");
        Console.WriteLine($"   Model: {_config.Model.ModelName}");
        if (!string.IsNullOrEmpty(_config.Model.BaseUrl))
        {
            Console.WriteLine($"   Base URL: {_config.Model.BaseUrl}");
        }

        Console.WriteLine();
        Console.WriteLine("Type 'help' for commands or 'exit' to quit.");
        Console.WriteLine("=========================================");
        Console.WriteLine();
    }

    /// <summary>
    /// Executes a user input through the default agent via the runtime facade.
    /// </summary>
    public async Task<string> ExecuteAsync(string input)
    {
        try
        {
            var context = new AgentContext
            {
                UserInput = input,
                WorkingDirectory = _basePath,
                AgentManager = _agentManager,
                Memory = _memoryStore,
                ToolDispatcher = new ToolDispatcher(new NullRuntimeEventSink(), _policyEngine)
            };

            var request = new AgentRunRequest
            {
                AgentName = ResolveDefaultAgentName(),
                Context = context
            };

            var result = await _runtime.RunAsync(request);

            if (result.Success)
            {
                return result.Output;
            }
            else
            {
                return $"Error: {result.Error}";
            }
        }
        catch (Exception ex)
        {
            return $"Error during execution: {ex.Message}";
        }
    }

    private string ResolveDefaultAgentName()
    {
        var configured = _config.DefaultAgentName;
        if (_agentManager.GetByName(configured) != null)
        {
            return configured;
        }

        var nonPlanner = _agentManager.GetAllAgents()
            .FirstOrDefault(a => !a.Name.Equals("PlannerAgent", StringComparison.OrdinalIgnoreCase));
        if (nonPlanner != null)
        {
            return nonPlanner.Name;
        }

        if (_agentManager.GetByName("PlannerAgent") != null)
        {
            return "PlannerAgent";
        }

        return configured;
    }

    private ILLMClient? CreatePlannerClient()
    {
        if (_config.Model.Provider.Equals("ollama", StringComparison.OrdinalIgnoreCase))
        {
            return new OllamaClient(_config.Model.ModelName);
        }

        return null;
    }

    /// <summary>
    /// Gets the permission service for checking capabilities.
    /// </summary>
    public PermissionService GetPermissionService()
    {
        return _permissionService;
    }

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    public AiomuxConfig GetConfig()
    {
        return _config;
    }

    /// <summary>
    /// Gets the agent manager.
    /// </summary>
    public IAgentManager GetAgentManager()
    {
        return _agentManager;
    }
}


