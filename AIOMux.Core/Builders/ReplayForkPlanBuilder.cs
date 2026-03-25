using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Core.Builders;

/// <summary>
/// Builds an ExecutionPlan from a forked/replayed execution context.
/// Used when continuing or modifying an existing run through a fork point.
/// 
/// Fork execution allows:
/// - Reconstructing state from execution records up to a selected step
/// - Continuing with new steps after the fork point
/// - Modifying execution path mid-run
/// 
/// Example usage:
/// <code>
/// var originalPlan = /* existing plan from source run */;
/// var forkedPlan = ExecutionPlanFactory.Fork(originalPlan, forkStepIndex: 5)
///     .WithDescription("Continuing from step 5 with alternative analysis")
///     .AddToolStep("altAnalysis", "AlternativeAnalyzer",
///         bindings: new() { ["input"] = "state.previousResult" })
///     .BuildAsync();
///
/// var context = new ExecutionContext
/// {
///     Services = new ExecutionRuntimeServices
///     {
///         AgentManager = agentManager,
///         Tools = tools,
///         ReplayMode = ReplayMode.Full
///     }
/// };
///
/// var runtime = new ExecutionRuntime();
/// var forkExecutor = new ForkReplayExecutor(runtime);
/// var result = await forkExecutor.ExecuteForkAsync(sourceRunId, 5, forkedPlan.Result, context);
/// </code>
/// </summary>
public class ReplayForkPlanBuilder : IExecutionPlanBuilder
{
    private readonly ExecutionPlan _originalPlan;
    private readonly int _forkStepIndex;
    private readonly List<ExecutionStep> _additionalSteps = new();
    private string? _description;

    /// <summary>
    /// Creates a new builder for a replay fork plan.
    /// </summary>
    /// <param name="originalPlan">The plan to fork from</param>
    /// <param name="forkStepIndex">Step index at which to fork reconstruction</param>
    public ReplayForkPlanBuilder(ExecutionPlan originalPlan, int forkStepIndex = 0)
    {
        _originalPlan = originalPlan ?? throw new ArgumentNullException(nameof(originalPlan));
        _forkStepIndex = forkStepIndex;
    }

    /// <summary>
    /// Adds additional tool steps after the fork point.
    /// </summary>
    /// <param name="stepId">Unique identifier for this step</param>
    /// <param name="toolName">Name of the tool to invoke</param>
    /// <param name="inputs">Static input values for the step</param>
    /// <param name="bindings">Bindings from context state into step inputs</param>
    /// <param name="outputKey">Optional key to store the step output in state</param>
    public ReplayForkPlanBuilder AddToolStep(string stepId, string toolName, Dictionary<string, object?>? inputs = null, Dictionary<string, string>? bindings = null, string? outputKey = null)
    {
        _additionalSteps.Add(new ExecutionStep
        {
            Id = stepId,
            Type = "tool",
            Target = toolName,
            Inputs = inputs ?? new(),
            Bindings = bindings ?? new(),
            OutputKey = outputKey
        });
        return this;
    }

    /// <summary>
    /// Adds additional agent steps after the fork point.
    /// </summary>
    /// <param name="stepId">Unique identifier for this step</param>
    /// <param name="agentName">Name of the agent to invoke</param>
    /// <param name="inputs">Static input values for the step</param>
    /// <param name="bindings">Bindings from context state into step inputs</param>
    /// <param name="outputKey">Optional key to store the step output in state</param>
    public ReplayForkPlanBuilder AddAgentStep(string stepId, string agentName, Dictionary<string, object?>? inputs = null, Dictionary<string, string>? bindings = null, string? outputKey = null)
    {
        _additionalSteps.Add(new ExecutionStep
        {
            Id = stepId,
            Type = "agent",
            Target = agentName,
            Inputs = inputs ?? new(),
            Bindings = bindings ?? new(),
            OutputKey = outputKey
        });
        return this;
    }

    /// <summary>
    /// Sets the fork plan description.
    /// </summary>
    public ReplayForkPlanBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Builds the ExecutionPlan for fork execution.
    /// The plan consists of original steps plus any additional steps added.
    /// </summary>
    public Task<ExecutionPlan> BuildAsync(CancellationToken cancellationToken = default)
    {
        var allSteps = new List<ExecutionStep>(_originalPlan.Steps);
        allSteps.AddRange(_additionalSteps);

        var planName = _originalPlan.Name;
        if (_additionalSteps.Count > 0)
            planName = $"{_originalPlan.Name}_fork";

        var plan = new ExecutionPlan
        {
            Name = planName,
            Description = _description ?? $"Fork of '{_originalPlan.Name}' at step index {_forkStepIndex}",
            Source = PlanSource.ReplayFork,
            Steps = allSteps.AsReadOnly(),
            Metadata = new Dictionary<string, object?>(_originalPlan.Metadata)
            {
                ["forkStepIndex"] = _forkStepIndex,
                ["sourceRunPlanName"] = _originalPlan.Name
            }
        };

        return Task.FromResult(plan);
    }
}
