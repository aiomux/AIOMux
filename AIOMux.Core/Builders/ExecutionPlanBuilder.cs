using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Core.Builders;

/// <summary>
/// Constructs an <see cref="ExecutionPlan"/> programmatically by accumulating steps.
/// Use when a plan must be assembled at runtime (for example, by solution-level agents or tests)
/// rather than loaded from a static JSON file.
/// Core executes the resulting plan. Planning decisions belong to agents outside Core.
/// </summary>
public class ExecutionPlanBuilder : IExecutionPlanBuilder
{
    private readonly string _planName;
    private readonly List<ExecutionStep> _steps = new();
    private string? _description;
    private readonly Dictionary<string, object?> _metadata = new();

    /// <summary>
    /// Creates a new builder for a programmatically assembled plan.
    /// </summary>
    /// <param name="planName">Name of the plan.</param>
    public ExecutionPlanBuilder(string planName)
    {
        if (string.IsNullOrWhiteSpace(planName))
            throw new ArgumentException("Plan name cannot be empty", nameof(planName));

        _planName = planName;
    }

    /// <summary>
    /// Adds a tool step to the plan.
    /// </summary>
    /// <param name="stepId">Unique identifier for this step.</param>
    /// <param name="toolName">Name of the tool to invoke.</param>
    /// <param name="inputs">Static input values for the step.</param>
    /// <param name="bindings">Bindings from context state into step inputs.</param>
    /// <param name="outputKey">Optional key to store the step output in state.</param>
    public ExecutionPlanBuilder AddToolStep(
        string stepId,
        string toolName,
        Dictionary<string, object?>? inputs = null,
        Dictionary<string, string>? bindings = null,
        string? outputKey = null)
    {
        _steps.Add(new ExecutionStep
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
    /// Adds an agent step to the plan.
    /// </summary>
    /// <param name="stepId">Unique identifier for this step.</param>
    /// <param name="agentName">Name of the agent to invoke.</param>
    /// <param name="inputs">Static input values for the step.</param>
    /// <param name="bindings">Bindings from context state into step inputs.</param>
    /// <param name="outputKey">Optional key to store the step output in state.</param>
    public ExecutionPlanBuilder AddAgentStep(
        string stepId,
        string agentName,
        Dictionary<string, object?>? inputs = null,
        Dictionary<string, string>? bindings = null,
        string? outputKey = null)
    {
        _steps.Add(new ExecutionStep
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
    /// Sets the plan description.
    /// </summary>
    public ExecutionPlanBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    /// <summary>
    /// Attaches arbitrary metadata to the plan for external consumers (for example: source tags, version annotations).
    /// Metadata is informational and does not affect runtime execution.
    /// </summary>
    public ExecutionPlanBuilder WithMetadata(string key, object? value)
    {
        _metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the <see cref="ExecutionPlan"/> from accumulated steps.
    /// </summary>
    public Task<ExecutionPlan> BuildAsync(CancellationToken cancellationToken = default)
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("Plan must have at least one step");

        var plan = new ExecutionPlan
        {
            Name = _planName,
            Description = _description,
            Source = PlanSource.Generated,
            Steps = _steps.AsReadOnly(),
            Metadata = new Dictionary<string, object?>(_metadata)
        };

        return Task.FromResult(plan);
    }
}
