using AIOMux.Connectors;
using AIOMux.Core;
using AIOMux.Core.Builders;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
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
    /// Scans declared assemblies for <c>IAgent</c> and <c>ITool</c> implementations,
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

        var agentManager = _agentManager ?? new AgentManager(_loggerFactory);
        var services = new ExecutionRuntimeServices
        {
            Tools = new Dictionary<string, ITool>(_tools ?? [], StringComparer.OrdinalIgnoreCase),
            AgentManager = agentManager,
            Options = BuildExecutionOptions(solution),
            PolicyEngine = LoadPolicyEngine(solution.PolicyConfig)
        };

        var connectors = ScanAndRegister(solution, services);

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
                    (evt, result) =>
                    {
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
    private IPolicyEngine LoadPolicyEngine(string? policyConfigPath)
    {
        if (string.IsNullOrWhiteSpace(policyConfigPath))
        {
            _logger?.LogInformation("No policy configuration specified, using AllowAllPolicyEngine");
            return new AllowAllPolicyEngine();
        }

        try
        {
            _logger?.LogInformation("Loading policy engine from {Path}", policyConfigPath);
            var json = File.ReadAllText(policyConfigPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<PolicyEngineConfig>(json, options);

            if (config == null || string.IsNullOrWhiteSpace(config.Type))
                return new AllowAllPolicyEngine();

            return config.Type.ToLower() switch
            {
                "allowall" => new AllowAllPolicyEngine(),
                _ => throw new InvalidOperationException($"Unknown policy engine type: {config.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load policy engine, defaulting to AllowAllPolicyEngine");
            return new AllowAllPolicyEngine();
        }
    }

    /// <summary>
    /// Registers built-in and assembly-provided agents and tools, then resolves declared connectors.
    /// </summary>
    private List<ResolvedConnector> ScanAndRegister(SolutionDefinition solution, ExecutionRuntimeServices services)
    {
        foreach (var agent in ScanAssemblyFor<IAgent>(typeof(IAgent).Assembly, _logger))
        {
            services.AgentManager?.Register(agent);
            _logger?.LogInformation("Discovered built-in agent '{Name}'", agent.Name);
        }

        foreach (var assemblyPath in solution.Assemblies)
        {
            foreach (var agent in ScanAssemblyFor<IAgent>(assemblyPath, _logger))
            {
                services.AgentManager?.Register(agent);
                _logger?.LogInformation("Discovered agent '{Name}' from {Path}", agent.Name, assemblyPath);
            }

            foreach (var tool in ScanAssemblyFor<ITool>(assemblyPath, _logger))
            {
                services.Tools[tool.Name] = tool;
                _logger?.LogInformation("Discovered tool '{Name}' from {Path}", tool.Name, assemblyPath);
            }
        }

        if (solution.Connectors.Count == 0)
            return [];

        var resolved = new List<ResolvedConnector>();
        foreach (var declaration in solution.Connectors)
        {
            var connector = BuiltInConnectorRegistry.Create(declaration.Type);
            resolved.Add(new ResolvedConnector
            {
                Connector = connector,
                Declaration = declaration
            });
            _logger?.LogInformation("Resolved connector '{Name}' (type '{Type}')", declaration.Name, declaration.Type);
        }

        return resolved;
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
