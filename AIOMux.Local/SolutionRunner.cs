using AIOMux.Clients;
using AIOMux.Core;
using AIOMux.Core.Builders;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text;
using System.Text.Json;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Local;

/// <summary>
/// Executes a solution end-to-end:
/// 1. Loads solution definition from solution.json
/// 2. Reads and builds the ExecutionPlan from entry point
/// 3. Creates ExecutionContext with appropriate configuration
/// 4. Executes through ExecutionRuntime
/// 5. Returns execution summary
/// A valid <c>policyConfig</c> path is required in the solution manifest; load fails if it is absent or the referenced file does not exist.
/// All tool execution is routed through <c>ToolDispatcher</c> with mandatory policy evaluation; no direct tool execution paths are supported.
/// </summary>
public class SolutionRunner
{
    private readonly ILogger<SolutionRunner>? _logger;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly IAgentManager? _agentManager;
    private readonly Dictionary<string, ITool>? _tools;

    /// <summary>
    /// Creates a new solution runner.
    /// </summary>
    /// <param name="logger">Optional logger</param>
    /// <param name="loggerFactory">Optional logger factory for creating runtime logger</param>
    /// <param name="agentManager">Optional agent manager for agent steps</param>
    /// <param name="tools">Optional pre-registered tools</param>
    public SolutionRunner(
        ILogger<SolutionRunner>? logger = null,
        ILoggerFactory? loggerFactory = null,
        IAgentManager? agentManager = null,
        Dictionary<string, ITool>? tools = null)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _agentManager = agentManager;
        _tools = tools ?? new();
    }

    /// <summary>
    /// Loads a solution from its solution.json file without executing it.
    /// Scans declared agent and tool packages for <c>IAgent</c> and <c>ITool</c> implementations,
    /// resolves declared connectors, and registers discovered agents and tools into the runtime services.
    /// </summary>
    /// <param name="solutionJsonPath">Path to solution.json</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The fully loaded solution with plan, services, and resolved connectors</returns>
    public async Task<LoadedSolution> LoadAsync(
        string solutionJsonPath,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Loading solution from {Path}", solutionJsonPath);
        var loader = new SolutionLoader(solutionJsonPath);
        var solution = loader.Load();
        loader.ValidateReferences(solution);

        _logger?.LogInformation("Loading execution plan from {Entry}", solution.Entry);
        var planJson = File.ReadAllText(solution.Entry);
        var planBuilder = new JsonExecutionPlanBuilder(planJson, planName: solution.Name);
        var plan = await planBuilder.BuildAsync(cancellationToken);

        var llmProfiles = BuildLlmProfileMap(solution);
        // Passing a non-null resolver, even with an empty map, ensures RequiredLlmProfiles validation always hard-fails for unsatisfied profiles during solution loading.
        var llmClientResolver = new LLMClientResolver((IReadOnlyDictionary<string, ILLMClient>)llmProfiles);

        var agentManager = _agentManager ?? new AgentManager(_loggerFactory);
        var services = new ExecutionRuntimeServices
        {
            Tools = new Dictionary<string, ITool>(_tools ?? [], StringComparer.OrdinalIgnoreCase),
            AgentManager = agentManager,
            LlmClientResolver = llmClientResolver,
            Options = BuildExecutionOptions(solution),
            PolicyEngine = LoadPolicyEngine(solution.PolicyConfig, solution.Mode)
        };

        var connectors = await ScanAndRegisterAsync(solution, services, llmClientResolver);

        return new LoadedSolution
        {
            Name = solution.Name,
            Description = solution.Description,
            WorkingDirectory = solution.WorkingDirectory ?? Path.GetDirectoryName(solution.Entry),
            EntryAgent = solution.EntryAgent,
            Plan = plan,
            Services = services,
            Connectors = connectors
        };
    }

    /// <summary>
    /// Runs a solution from its solution.json file.
    /// </summary>
    /// <param name="solutionJsonPath">Path to solution.json</param>
    /// <param name="input">Initial input to the execution plan</param>
    /// <param name="additionalInputs">Optional additional inputs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary of execution result</returns>
    public async Task<SolutionExecutionSummary> RunAsync(
        string solutionJsonPath,
        string input,
        Dictionary<string, object?>? additionalInputs = null,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var summary = new SolutionExecutionSummary();

        try
        {
            var loaded = await LoadAsync(solutionJsonPath, cancellationToken);

            summary.SolutionName = loaded.Name;
            summary.SolutionDescription = loaded.Description;
            summary.PlanName = loaded.Plan.Name;
            summary.PlanSource = loaded.Plan.Source;
            summary.StepCount = loaded.Plan.Steps.Count;

            _logger?.LogInformation("Creating execution context");
            var ctx = new ExecutionContext
            {
                RunId = Guid.NewGuid().ToString(),
                WorkingDirectory = loaded.WorkingDirectory,
                Services = loaded.Services
            };

            ctx.Inputs["input"] = input;
            if (additionalInputs != null)
            {
                foreach (var (key, value) in additionalInputs)
                    ctx.Inputs[key] = value;
            }

            var availableOperations = loaded.Services.Tools
                .ToDictionary(
                    t => t.Key,
                    t => t.Value.Descriptor.Operations
                        .Select(op => op.Name)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            ctx.Inputs["available_operations"] = availableOperations;

            _logger?.LogInformation("Starting execution of plan '{PlanName}' with {StepCount} steps",
                loaded.Plan.Name, loaded.Plan.Steps.Count);

            var runtimeLogger = _loggerFactory?.CreateLogger<ExecutionRuntime>();
            var runtime = new ExecutionRuntime(logger: runtimeLogger);
            var result = await runtime.ExecuteAsync(loaded.Plan, ctx, cancellationToken);

            sw.Stop();

            summary.RunId = ctx.RunId;
            summary.Success = result.Success;
            summary.Output = result.Output;
            summary.Error = result.Error;
            summary.DurationMs = sw.Elapsed.TotalMilliseconds;
            summary.ExecutedSteps = ctx.Records.Count;
            summary.Records = ctx.Records.ToList();

            if (result.Success)
                _logger?.LogInformation("Solution '{SolutionName}' completed successfully in {Ms:F0}ms",
                    loaded.Name, sw.Elapsed.TotalMilliseconds);
            else
                _logger?.LogError("Solution '{SolutionName}' failed: {Error}", loaded.Name, result.Error);

            return summary;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(ex, "Solution execution failed");
            summary.Success = false;
            summary.Error = ex.Message;
            summary.DurationMs = sw.Elapsed.TotalMilliseconds;
            return summary;
        }
    }

    /// <summary>
    /// Replays a prior run by reconstructing state up to the final step and executing with replayed tool outputs.
    /// </summary>
    /// <param name="solutionJsonPath">Path to solution.json</param>
    /// <param name="sourceRunId">Source run id to replay from</param>
    /// <param name="input">Optional input override</param>
    /// <param name="additionalInputs">Optional additional inputs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<SolutionExecutionSummary> ReplayAsync(
        string solutionJsonPath,
        string sourceRunId,
        string? input = null,
        Dictionary<string, object?>? additionalInputs = null,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(solutionJsonPath, cancellationToken);
        if (loaded.Plan.Steps.Count == 0)
        {
            return new SolutionExecutionSummary
            {
                SolutionName = loaded.Name,
                SolutionDescription = loaded.Description,
                PlanName = loaded.Plan.Name,
                PlanSource = loaded.Plan.Source,
                StepCount = 0,
                Success = false,
                Error = "Cannot replay a plan with no steps."
            };
        }

        var replayStepIndex = loaded.Plan.Steps.Count - 1;
        return await ExecuteForkLikeRunAsync(
            loaded,
            loaded.Plan,
            sourceRunId,
            replayStepIndex,
            input,
            additionalInputs,
            cancellationToken);
    }

    /// <summary>
    /// Forks a prior run at a selected step and continues execution using the fork plan.
    /// </summary>
    /// <param name="solutionJsonPath">Path to solution.json</param>
    /// <param name="sourceRunId">Source run id to fork from</param>
    /// <param name="forkStepIndex">Step index at which to reconstruct state</param>
    /// <param name="input">Optional input override</param>
    /// <param name="additionalInputs">Optional additional inputs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<SolutionExecutionSummary> ForkAsync(
        string solutionJsonPath,
        string sourceRunId,
        int forkStepIndex,
        string? input = null,
        Dictionary<string, object?>? additionalInputs = null,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(solutionJsonPath, cancellationToken);
        var forkPlan = await ExecutionPlanFactory
            .Fork(loaded.Plan, forkStepIndex)
            .BuildAsync(cancellationToken);

        return await ExecuteForkLikeRunAsync(
            loaded,
            forkPlan,
            sourceRunId,
            forkStepIndex,
            input,
            additionalInputs,
            cancellationToken);
    }

    private async Task<SolutionExecutionSummary> ExecuteForkLikeRunAsync(
        LoadedSolution loaded,
        ExecutionPlan plan,
        string sourceRunId,
        int stepIndex,
        string? input,
        Dictionary<string, object?>? additionalInputs,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var summary = new SolutionExecutionSummary
        {
            SolutionName = loaded.Name,
            SolutionDescription = loaded.Description,
            PlanName = plan.Name,
            PlanSource = plan.Source,
            StepCount = plan.Steps.Count
        };

        try
        {
            var services = loaded.Services;
            if (services.ReplayMode == ReplayMode.None)
                services.ReplayMode = ReplayMode.ToolsOnly;

            var ctx = new ExecutionContext
            {
                RunId = Guid.NewGuid().ToString(),
                WorkingDirectory = loaded.WorkingDirectory,
                Services = services
            };

            if (input != null)
                ctx.Inputs["input"] = input;

            if (additionalInputs != null)
            {
                foreach (var (key, value) in additionalInputs)
                    ctx.Inputs[key] = value;
            }

            var runtimeLogger = _loggerFactory?.CreateLogger<ExecutionRuntime>();
            var runtime = new ExecutionRuntime(logger: runtimeLogger);
            var forkExecutor = new ForkReplayExecutor(runtime);
            var result = await forkExecutor.ExecuteForkAsync(sourceRunId, stepIndex, plan, ctx, cancellationToken);

            sw.Stop();

            summary.RunId = ctx.RunId;
            summary.Success = result.Success;
            summary.Output = result.Output;
            summary.Error = result.Error;
            summary.DurationMs = sw.Elapsed.TotalMilliseconds;
            summary.ExecutedSteps = ctx.Records.Count;
            summary.Records = ctx.Records.ToList();

            return summary;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger?.LogError(ex, "Replay/fork execution failed");
            summary.Success = false;
            summary.Error = ex.Message;
            summary.DurationMs = sw.Elapsed.TotalMilliseconds;
            return summary;
        }
    }

    /// <summary>
    /// Loads a solution and starts all discovered connectors in serve mode.
    /// Each connector runs until the cancellation token is signalled.
    /// </summary>
    /// <param name="solutionJsonPath">Path to solution.json</param>
    /// <param name="cancellationToken">Token used to stop the serve loop</param>
    public async Task ServeAsync(
        string solutionJsonPath,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(solutionJsonPath, cancellationToken);

        if (loaded.Connectors.Count == 0)
        {
            Console.Error.WriteLine("No connectors found in solution. Nothing to serve.");
            return;
        }

        var runtimeLogger = _loggerFactory?.CreateLogger<ExecutionRuntime>();
        var runtime = new ExecutionRuntime(logger: runtimeLogger);

        _logger?.LogInformation("Starting {Count} connector(s) in serve mode", loaded.Connectors.Count);

        var tasks = loaded.Connectors
            .Select(resolved =>
            {
                var context = new ConnectorContext(
                    runtime,
                    loaded.Plan,
                    loaded.Services,
                    loaded.EntryAgent,
                    resolved.Declaration.Config,
                    loaded.WorkingDirectory,
                    (evt, result) =>
                    {
                        var inputText = evt.Payload?.ToString()?.Trim() ?? string.Empty;
                        Console.WriteLine($"[Connector:console-input] Received input: {inputText}");

                        if (result.Success)
                        {
                            var text = !string.IsNullOrWhiteSpace(result.Output)
                                ? result.Output.Trim()
                                : evt.Payload?.ToString()?.Trim();

                            if (string.IsNullOrWhiteSpace(text))
                                text = "<no output>";

                            Console.WriteLine($"[{resolved.Declaration.Name}] {text}");
                        }
                        else
                        {
                            Console.Error.WriteLine($"Connector execution failed ({resolved.Declaration.Name}): {result.Error}");
                        }
                    });

                return resolved.Connector.StartAsync(context, cancellationToken);
            })
            .ToList();

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Loads the policy engine based on solution configuration.
    /// </summary>
    private IPolicyEngine LoadPolicyEngine(string? policyConfigPath, ExecutionMode mode = ExecutionMode.Development)
    {
        if (string.IsNullOrWhiteSpace(policyConfigPath))
            throw new InvalidOperationException("Policy configuration is required. Set 'policyConfig' in solution.json.");

        _logger?.LogInformation("Loading policy configuration from {Path}", policyConfigPath);
        var json = File.ReadAllText(policyConfigPath);

        // Support direct operation policy documents:
        // {
        //   "version": "1",
        //   "tools": { ... }
        // }
        try
        {
            var operationDocument = OperationPolicyDocument.Deserialize(json);
            _logger?.LogInformation("Loaded operation policy document from {Path}", policyConfigPath);
            return OperationPolicyEngine.FromDocument(operationDocument);
        }
        catch (Exception)
        {
            // Not a direct operation policy document. Continue with engine-config format.
        }

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var config = JsonSerializer.Deserialize<PolicyEngineConfig>(json, options)
            ?? throw new InvalidOperationException("Policy configuration file is empty or invalid.");

        if (string.IsNullOrWhiteSpace(config.Type))
            throw new InvalidOperationException("Policy configuration must include a non-empty 'type', or use an operation policy document with 'version' and 'tools'.");

        return config.Type.ToLowerInvariant() switch
        {
            "allowall" => CreateAllowAllPolicyEngine(mode),
            "tooldenylist" => new ToolDenyListPolicyEngine(ResolveDeniedTools(config.Parameters)),
            "operationpolicy" => LoadOperationPolicyEngine(config.Parameters, policyConfigPath),
            _ => throw new InvalidOperationException($"Unknown policy engine type: {config.Type}")
        };
    }

    /// <summary>
    /// Creates an <see cref="AllowAllPolicyEngine"/> after validating that the current
    /// environment and execution mode permit its use.
    /// Throws <see cref="InvalidOperationException"/> if the AIOMUX_DISABLE_ALLOWALL
    /// environment variable is set to "true" or if the solution is running in Production mode.
    /// </summary>
    private AllowAllPolicyEngine CreateAllowAllPolicyEngine(ExecutionMode mode)
    {
        var disableEnv = Environment.GetEnvironmentVariable("AIOMUX_DISABLE_ALLOWALL");
        if (string.Equals(disableEnv, "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "AllowAll policy is disabled by the AIOMUX_DISABLE_ALLOWALL environment variable. " +
                "Remove the AllowAll policy configuration or unset the environment variable.");

        if (mode == ExecutionMode.Production)
            throw new InvalidOperationException(
                "AllowAll policy is not permitted in Production mode. " +
                "Use a restrictive policy engine or change the execution mode to Development.");

        ILogger? logger = _loggerFactory?.CreateLogger<AllowAllPolicyEngine>();
        return new AllowAllPolicyEngine(logger);
    }

    private static IEnumerable<string> ResolveDeniedTools(Dictionary<string, object?> parameters)
    {
        if (!parameters.TryGetValue("denyTools", out var denyTools))
            return [];

        return ToolDenyListPolicyEngine.ParseDeniedTools(denyTools);
    }

    /// <summary>
    /// Loads an <see cref="OperationPolicyEngine"/> from a standalone policy document.
    /// The parameters map must contain a <c>policyFile</c> entry with an absolute or
    /// config-directory-relative path to a JSON file matching the <see cref="OperationPolicyDocument"/> schema.
    /// Throws <see cref="InvalidOperationException"/> when the parameter is missing or the file cannot be parsed.
    /// </summary>
    private OperationPolicyEngine LoadOperationPolicyEngine(
        Dictionary<string, object?> parameters,
        string? configPath)
    {
        if (!parameters.TryGetValue("policyFile", out var raw) || raw is not string relPath || string.IsNullOrWhiteSpace(relPath))
            throw new InvalidOperationException("operationpolicy engine requires a 'policyFile' parameter.");

        var basePath = string.IsNullOrWhiteSpace(configPath)
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? Directory.GetCurrentDirectory();

        var policyFilePath = Path.IsPathRooted(relPath)
            ? relPath
            : Path.Combine(basePath, relPath);

        _logger?.LogInformation("Loading operation policy from {Path}", policyFilePath);

        // JsonException propagates on unknown enum values or malformed JSON; caller logs and re-throws.
        var document = OperationPolicyDocument.LoadFromFile(policyFilePath);
        return OperationPolicyEngine.FromDocument(document);
    }

    /// <summary>
    /// Registers built-in and package-provided agents and tools, then resolves declared connectors.
    /// Agent packages are loaded through <see cref="IAgentManager.LoadAgentsFromAssemblyAsync(string, ILLMClientResolver?, Dictionary{string, object}?)"/>
    /// so each discovered agent can resolve its preferred LLM profile.
    /// </summary>
    private async Task<List<ResolvedConnector>> ScanAndRegisterAsync(
        SolutionDefinition solution,
        ExecutionRuntimeServices services,
        ILLMClientResolver llmClientResolver)
    {
        foreach (var agent in ScanAssemblyFor<IAgent>(typeof(IAgent).Assembly, _logger))
        {
            services.AgentManager?.Register(agent);
            _logger?.LogInformation("Discovered built-in agent '{Name}'", agent.Name);
        }

        foreach (var tool in ScanAssemblyForTools(typeof(ITool).Assembly, typeof(ITool).Assembly.Location, llmClientResolver, _logger))
        {
            if (tool is not DispatchableToolBase)
                throw new InvalidOperationException($"Tool '{tool.Name}' must inherit DispatchableToolBase.");

            if (!services.Tools.TryAdd(tool.Name, tool))
                throw new InvalidOperationException($"Duplicate tool ID '{tool.Name}' detected while registering built-in tools.");

            LogRegisteredTool(tool, _logger);
        }

        foreach (var agentPackage in solution.Agents)
        {
            ValidateRolePackage(agentPackage, expectedRole: "agent");

            if (services.AgentManager != null)
            {
                var loaded = await services.AgentManager.LoadAgentsFromAssemblyAsync(agentPackage, llmClientResolver);
                if (!loaded)
                    throw new InvalidOperationException($"Agent package '{agentPackage}' did not load any agents.");
            }
        }

        foreach (var toolPackage in solution.Tools)
        {
            ValidateRolePackage(toolPackage, expectedRole: "tool");

            var toolAssembly = Assembly.LoadFrom(toolPackage);
            var discoveredTools = ScanAssemblyForTools(toolAssembly, toolPackage, llmClientResolver, _logger).ToList();
            if (discoveredTools.Count == 0 && toolAssembly.GetTypes().Any(t => t.IsClass && !t.IsAbstract && typeof(ITool).IsAssignableFrom(t)))
            {
                throw new InvalidOperationException($"Tool package '{toolPackage}' contains ITool implementations but produced zero tool instances.");
            }

            foreach (var tool in discoveredTools)
            {
                if (tool is not DispatchableToolBase)
                    throw new InvalidOperationException($"Tool '{tool.Name}' from '{toolPackage}' must inherit DispatchableToolBase.");

                if (!services.Tools.TryAdd(tool.Name, tool))
                    throw new InvalidOperationException($"Duplicate tool ID '{tool.Name}' detected while registering package '{toolPackage}'.");

                LogRegisteredTool(tool, _logger);
            }
        }

        var discoveredConnectorTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var connectorPackage in solution.Connectors)
        {
            ValidateRolePackage(connectorPackage, expectedRole: "connector");

            foreach (var connector in ScanAssemblyFor<IConnector>(connectorPackage, _logger))
            {
                var connectorName = connector.Name?.Trim();
                if (string.IsNullOrWhiteSpace(connectorName))
                    throw new InvalidOperationException($"Connector implementation '{connector.GetType().FullName}' from '{connectorPackage}' returned an empty Name.");

                var implementationType = connector.GetType();
                if (discoveredConnectorTypes.TryGetValue(connectorName, out var existingType) && existingType != implementationType)
                {
                    throw new InvalidOperationException(
                        $"Connector name '{connectorName}' is declared by multiple implementations: '{existingType.FullName}' and '{implementationType.FullName}'.");
                }

                discoveredConnectorTypes[connectorName] = implementationType;
                _logger?.LogInformation("Discovered connector '{Name}' from {Path}", connectorName, connectorPackage);
            }
        }

        if (solution.ConnectorConfigurations.Count == 0)
            return [];

        var resolved = new List<ResolvedConnector>();
        foreach (var declaration in solution.ConnectorConfigurations)
        {
            if (!discoveredConnectorTypes.TryGetValue(declaration.Type, out var connectorType))
            {
                throw new InvalidOperationException(
                    $"Connector '{declaration.Name}' declares unknown type '{declaration.Type}'. Declare the connector DLL in the 'connectors' list and ensure it exposes an IConnector named '{declaration.Type}'.");
            }

            if (Activator.CreateInstance(connectorType) is not IConnector connector)
            {
                throw new InvalidOperationException(
                    $"Could not create connector '{declaration.Type}' from type '{connectorType.FullName}'.");
            }

            resolved.Add(new ResolvedConnector
            {
                Connector = connector,
                Declaration = declaration
            });
            _logger?.LogInformation("Resolved connector '{Name}' (type '{Type}')", declaration.Name, declaration.Type);
        }

        return resolved;
    }

    private static Dictionary<string, ILLMClient> BuildLlmProfileMap(SolutionDefinition solution)
    {
        var profiles = new Dictionary<string, ILLMClient>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, config) in solution.LlmProfiles)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Invalid profile config: profile name cannot be empty.");

            var client = CreateLlmClient(config, name);
            if (!profiles.TryAdd(name, client))
                throw new InvalidOperationException($"Duplicate LLM profile '{name}' is not allowed.");
        }

        return profiles;
    }

    private static ILLMClient CreateLlmClient(LlmConfiguration config, string profileName)
    {
        if (string.IsNullOrWhiteSpace(config.Provider))
            throw new InvalidOperationException($"Invalid profile config '{profileName}': provider is required.");

        if (config.MaxRequestsPerMinute < 0)
            throw new InvalidOperationException($"Invalid profile config '{profileName}': maxRequestsPerMinute must be greater than or equal to zero.");

        var provider = config.Provider.Trim().ToLowerInvariant();
        return provider switch
        {
            "ollama" => CreateOllamaClient(config, profileName),
            "openai" => CreateOpenAIClient(config, profileName),
            _ => throw new InvalidOperationException($"Invalid profile config '{profileName}': unknown provider '{config.Provider}'. Supported providers: 'ollama', 'openai'.")
        };
    }

    private static ILLMClient CreateOllamaClient(LlmConfiguration config, string profileName)
    {
        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException($"Invalid profile config '{profileName}': ollama model is required.");

        if (string.IsNullOrWhiteSpace(config.Endpoint))
            throw new InvalidOperationException($"Invalid profile config '{profileName}': ollama endpoint is required.");

        var maxRequestsPerMinute = config.MaxRequestsPerMinute > 0 ? config.MaxRequestsPerMinute : 60;

        return new OllamaClient(config.Model, maxRequestsPerMinute, config.Endpoint);
    }

    private static ILLMClient CreateOpenAIClient(LlmConfiguration config, string profileName)
    {
        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException($"Invalid profile config '{profileName}': openai model is required.");

        var apiKey = string.Empty;

        if (!string.IsNullOrWhiteSpace(config.ApiKeyEnvironmentVariable))
            apiKey = Environment.GetEnvironmentVariable(config.ApiKeyEnvironmentVariable) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(config.ApiKey))
            apiKey = config.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            var hint = string.IsNullOrWhiteSpace(config.ApiKeyEnvironmentVariable)
                ? "Set 'apiKeyEnvironmentVariable' or 'apiKey' in the LLM profile."
                : $"Environment variable '{config.ApiKeyEnvironmentVariable}' is not set or empty. Set it or use 'apiKey' in the LLM profile.";
            throw new InvalidOperationException($"Invalid profile config '{profileName}': openai API key is required. {hint}");
        }

        var baseUrl = string.IsNullOrWhiteSpace(config.Endpoint) ? null : config.Endpoint;
        var maxRequestsPerMinute = config.MaxRequestsPerMinute > 0 ? config.MaxRequestsPerMinute : 60;

        return new OpenAIClient(apiKey, config.Model, baseUrl, maxRequestsPerMinute);
    }

    /// <summary>
    /// Scans a pre-loaded assembly and returns all non-abstract instances of <typeparamref name="T"/>
    /// that can be created with a parameterless constructor.
    /// </summary>
    private static IEnumerable<T> ScanAssemblyFor<T>(Assembly assembly, ILogger? logger)
    {
        var results = new List<T>();
        foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(T).IsAssignableFrom(t)))
        {
            try
            {
                if (Activator.CreateInstance(type) is T instance)
                    results.Add(instance);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Could not instantiate {Type}", type.FullName);
            }
        }
        return results;
    }

    /// <summary>
    /// Loads an assembly and returns all non-abstract instances of <typeparamref name="T"/>
    /// that can be created with a parameterless constructor.
    /// </summary>
    private static IEnumerable<T> ScanAssemblyFor<T>(string assemblyPath, ILogger? logger)
    {
        Assembly assembly;
        try
        {
            assembly = Assembly.LoadFrom(assemblyPath);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Could not load assembly for scanning: {Path}", assemblyPath);
            return [];
        }

        return ScanAssemblyFor<T>(assembly, logger);
    }

    private static IEnumerable<ITool> ScanAssemblyForTools(
        Assembly assembly,
        string assemblyPath,
        ILLMClientResolver llmClientResolver,
        ILogger? logger)
    {
        var toolTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ITool).IsAssignableFrom(t))
            .ToList();

        if (toolTypes.Count == 0)
            return [];

        var results = new List<ITool>();
        foreach (var toolType in toolTypes)
        {
            var tool = ActivateToolInstance(toolType, assemblyPath, llmClientResolver);
            results.Add(tool);
            logger?.LogInformation("Discovered tool type '{ToolType}' from {Path}", toolType.FullName, assemblyPath);
        }

        if (results.Count == 0)
            throw new InvalidOperationException($"Tool package '{assemblyPath}' contains ITool implementations but produced zero tool instances.");

        return results;
    }

    private static ITool ActivateToolInstance(Type toolType, string assemblyPath, ILLMClientResolver llmClientResolver)
    {
        var parameterlessCtor = toolType.GetConstructor(Type.EmptyTypes);
        var resolverCtor = toolType.GetConstructor([typeof(ILLMClientResolver)]);

        var supportedSignatures = "ToolType() or ToolType(ILLMClientResolver)";

        if (resolverCtor != null)
        {
            try
            {
                if (resolverCtor.Invoke([llmClientResolver]) is ITool resolverActivated)
                    return resolverActivated;

                throw new InvalidOperationException($"Type '{toolType.FullName}' did not create an ITool instance when invoked with ILLMClientResolver.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Tool activation failed. Assembly: '{assemblyPath}'. Tool type: '{toolType.FullName}'. Supported constructor signatures: {supportedSignatures}.",
                    ex.InnerException ?? ex);
            }
        }

        if (parameterlessCtor != null)
        {
            try
            {
                if (parameterlessCtor.Invoke([]) is ITool parameterlessActivated)
                    return parameterlessActivated;

                throw new InvalidOperationException($"Type '{toolType.FullName}' did not create an ITool instance when invoked with parameterless constructor.");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Tool activation failed. Assembly: '{assemblyPath}'. Tool type: '{toolType.FullName}'. Supported constructor signatures: {supportedSignatures}.",
                    ex.InnerException ?? ex);
            }
        }

        throw new InvalidOperationException(
            $"Tool activation failed. Assembly: '{assemblyPath}'. Tool type: '{toolType.FullName}'. Supported constructor signatures: {supportedSignatures}. No supported constructor was found.");
    }

    private static void LogRegisteredTool(ITool tool, ILogger? logger)
    {
        logger?.LogInformation("Registered tool: {ToolId}", tool.Name);
        logger?.LogInformation("Operations:");
        foreach (var operation in tool.Descriptor.Operations)
        {
            logger?.LogInformation("- {Operation}", operation.Name);
        }
    }

    private static void ValidateRolePackage(string packagePath, string expectedRole)
    {
        var assembly = Assembly.LoadFrom(packagePath);

        var hasAgent = assembly.GetTypes().Any(t => t.IsClass && !t.IsAbstract && typeof(IAgent).IsAssignableFrom(t));
        var hasTool = assembly.GetTypes().Any(t => t.IsClass && !t.IsAbstract && typeof(ITool).IsAssignableFrom(t));
        var hasConnector = assembly.GetTypes().Any(t => t.IsClass && !t.IsAbstract && typeof(IConnector).IsAssignableFrom(t));

        switch (expectedRole)
        {
            case "agent":
                if (!hasAgent)
                    throw new InvalidOperationException($"Agent package '{packagePath}' does not contain any agent implementations.");
                if (hasTool)
                    throw new InvalidOperationException($"Agent package '{packagePath}' contains tool implementations, which is not allowed.");
                if (hasConnector)
                    throw new InvalidOperationException($"Agent package '{packagePath}' contains connector implementations, which is not allowed.");
                break;
            case "tool":
                if (!hasTool)
                    throw new InvalidOperationException($"Tool package '{packagePath}' does not contain any tool implementations.");
                if (hasAgent)
                    throw new InvalidOperationException($"Tool package '{packagePath}' contains agent implementations, which is not allowed.");
                if (hasConnector)
                    throw new InvalidOperationException($"Tool package '{packagePath}' contains connector implementations, which is not allowed.");
                break;
            case "connector":
                if (hasAgent)
                    throw new InvalidOperationException($"Connector package '{packagePath}' contains agent implementations, which is not allowed.");
                if (hasTool)
                    throw new InvalidOperationException($"Connector package '{packagePath}' contains tool implementations, which is not allowed.");
                if (!hasConnector)
                    throw new InvalidOperationException($"Connector package '{packagePath}' does not contain any connector implementations.");
                break;
        }
    }

    private static ExecutionOptions BuildExecutionOptions(SolutionDefinition solution) => new()
    {
        CollectMetrics = solution.ExecutionOptions?.CollectMetrics ?? true,
        GenerateJobSummary = solution.ExecutionOptions?.GenerateJobSummary ?? true,
        IncludeDetailedMetrics = solution.ExecutionOptions?.IncludeDetailedMetrics ?? false
    };
}

/// <summary>
/// Policy engine configuration.
/// </summary>
public sealed class PolicyEngineConfig
{
    /// <summary>
    /// Type of policy engine (e.g., "allowall").
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Additional configuration parameters.
    /// </summary>
    public Dictionary<string, object?> Parameters { get; set; } = new();
}

/// <summary>
/// Summary of a solution execution.
/// </summary>
public sealed class SolutionExecutionSummary
{
    /// <summary>
    /// Unique identifier for this run.
    /// </summary>
    public string? RunId { get; set; }

    /// <summary>
    /// Name of the solution that was executed.
    /// </summary>
    public string? SolutionName { get; set; }

    /// <summary>
    /// Description of the solution.
    /// </summary>
    public string? SolutionDescription { get; set; }

    /// <summary>
    /// Name of the execution plan.
    /// </summary>
    public string? PlanName { get; set; }

    /// <summary>
    /// Source of the plan (Static, Generated, ReplayFork).
    /// </summary>
    public PlanSource PlanSource { get; set; }

    /// <summary>
    /// Total steps in the plan.
    /// </summary>
    public int StepCount { get; set; }

    /// <summary>
    /// Number of steps actually executed.
    /// </summary>
    public int ExecutedSteps { get; set; }

    /// <summary>
    /// Whether the execution succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Final output from the execution.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Error message if execution failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public double DurationMs { get; set; }

    /// <summary>
    /// Execution records for all steps.
    /// </summary>
    public List<ExecutionRecord> Records { get; set; } = new();

    /// <summary>
    /// Returns a formatted string representation of the summary.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Solution: {SolutionName}");
        if (!string.IsNullOrWhiteSpace(SolutionDescription))
            sb.AppendLine($"Description: {SolutionDescription}");
        sb.AppendLine($"Plan: {PlanName} (Source: {PlanSource})");
        sb.AppendLine($"Steps: {ExecutedSteps}/{StepCount}");
        sb.AppendLine($"Status: {(Success ? "Success" : "Failed")}");
        if (!string.IsNullOrWhiteSpace(Error))
            sb.AppendLine($"Error: {Error}");
        sb.AppendLine($"Duration: {DurationMs:F0}ms");
        return sb.ToString();
    }
}
