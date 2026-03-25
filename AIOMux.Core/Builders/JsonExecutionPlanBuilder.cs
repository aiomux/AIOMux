using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using System.Text.Json;

namespace AIOMux.Core.Builders;

/// <summary>
/// Builds an ExecutionPlan from a JSON definition.
// 
/// Typical JSON structure:
/// <code>
/// {
///   "name": "MyPlan",
///   "description": "Plan description",
///   "source": "Static",
///   "steps":
///     {
///       "id": "step1",
///       "type": "tool",
///       "target": "WebSearch",
///       "inputs": { "query": "latest news" }
///     },
///     {
///       "id": "step2",
///       "type": "tool", 
///       "target": "Summarizer",
///       "bindings": { "input": "state.step1" },
///       "outputKey": "summary"
///     }
///   ]
/// }
/// </code>
/// 
/// Example usage:
/// <code>
/// var builder = ExecutionPlanFactory.FromJson(jsonContent);
/// var plan = await builder.BuildAsync();
/// </code>
/// </summary>
public class JsonExecutionPlanBuilder : IExecutionPlanBuilder
{
    private readonly string _json;
    private readonly string? _planName;

    /// <summary>
    /// Creates a new builder from JSON content.
    /// </summary>
    /// <param name="json">JSON definition of the plan</param>
    /// <param name="planName">Optional name to override plan name in JSON</param>
    public JsonExecutionPlanBuilder(string json, string? planName = null)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON content cannot be empty", nameof(json));

        _json = json;
        _planName = planName;
    }

    /// <summary>
    /// Builds the ExecutionPlan from JSON.
    /// </summary>
    public Task<ExecutionPlan> BuildAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var plan = JsonSerializer.Deserialize<ExecutionPlan>(_json, options)
                ?? throw new InvalidOperationException("Failed to deserialize plan from JSON");

            // Override plan name if provided
            if (!string.IsNullOrWhiteSpace(_planName))
                plan.Name = _planName;

            // Ensure source is set
            if (plan.Source == PlanSource.Static)
                plan.Source = PlanSource.Static;

            return Task.FromResult(plan);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Invalid JSON for plan definition", ex);
        }
    }
}
