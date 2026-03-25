using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using Microsoft.Extensions.Logging;
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
            // Load the solution definition.
            _logger?.LogInformation("Loading solution from {Path}", solutionJsonPath);
            var loader = new SolutionLoader(solutionJsonPath);
            var solution = loader.Load();
            loader.ValidateReferences(solution);

            summary.SolutionName = solution.Name;
            summary.SolutionDescription = solution.Description;

            // Load and build the execution plan.
            _logger?.LogInformation("Loading execution plan from {Entry}", solution.Entry);
            var planJson = File.ReadAllText(solution.Entry);
            var plan = await ExecutionPlanFactory
                .FromJson(planJson, planName: solution.Name)
                .BuildAsync(cancellationToken);

            summary.PlanName = plan.Name;
            summary.PlanSource = plan.Source;
            summary.StepCount = plan.Steps.Count;

            // Create the execution context.
            _logger?.LogInformation("Creating execution context");
            var ctx = new ExecutionContext
            {
                RunId = Guid.NewGuid().ToString(),
                WorkingDirectory = solution.WorkingDirectory ?? Path.GetDirectoryName(solution.Entry),
                Tools = _tools,
                AgentManager = _agentManager,
                Options = new AIOMux.Core.Models.ExecutionOptions
                {
                    CollectMetrics = solution.ExecutionOptions?.CollectMetrics ?? true,
                    GenerateJobSummary = solution.ExecutionOptions?.GenerateJobSummary ?? true,
                    IncludeDetailedMetrics = solution.ExecutionOptions?.IncludeDetailedMetrics ?? false
                },
                PolicyEngine = LoadPolicyEngine(solution.PolicyConfig)
            };

            // Add input values to the context.
            ctx.Inputs["input"] = input;
            if (additionalInputs != null)
            {
                foreach (var (key, value) in additionalInputs)
                    ctx.Inputs[key] = value;
            }

            // Execute the plan through the runtime.
            _logger?.LogInformation("Starting execution of plan '{PlanName}' with {StepCount} steps",
                plan.Name, plan.Steps.Count);

            var runtimeLogger = _loggerFactory?.CreateLogger<ExecutionRuntime>();
            var runtime = new ExecutionRuntime(logger: runtimeLogger);
            var request = new AIOMux.Core.Models.ExecutionRunRequest { Plan = plan, Context = ctx };
            var result = await runtime.RunAsync(request, cancellationToken);

            sw.Stop();

            // Build the execution summary.
            summary.RunId = ctx.RunId;
            summary.Success = result.Success;
            summary.Output = result.Output;
            summary.Error = result.Error;
            summary.DurationMs = sw.Elapsed.TotalMilliseconds;
            summary.ExecutedSteps = ctx.Records.Count;
            summary.Records = ctx.Records.ToList();

            if (result.Success)
                _logger?.LogInformation("Solution '{SolutionName}' completed successfully in {Ms:F0}ms",
                    solution.Name, sw.Elapsed.TotalMilliseconds);
            else
                _logger?.LogError("Solution '{SolutionName}' failed: {Error}",
                    solution.Name, result.Error);

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
