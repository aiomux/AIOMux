using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Core;

/// <summary>
/// Convenience factory for creating execution plans via builders.
/// Provides fluent API entry points for all plan sources.
/// </summary>
public static class ExecutionPlanFactory
{
    /// <summary>
    /// Creates a plan from JSON content (static plan).
    /// </summary>
    /// <param name="json">JSON definition of the plan</param>
    /// <param name="planName">Optional name to override JSON plan name</param>
    /// <returns>Builder ready to call BuildAsync</returns>
    public static IExecutionPlanBuilder FromJson(string json, string? planName = null)
        => new JsonExecutionPlanBuilder(json, planName);

    /// <summary>
    /// Creates a plan builder for generated/dynamic plans.
    /// </summary>
    /// <param name="planName">Name of the generated plan</param>
    /// <returns>Builder with fluent methods to add steps</returns>
    public static PlannerExecutionPlanBuilder Create(string planName)
        => new PlannerExecutionPlanBuilder(planName);

    /// <summary>
    /// Creates a plan builder for forking an existing run.
    /// </summary>
    /// <param name="originalPlan">The plan to fork from</param>
    /// <param name="forkEventIndex">Event index at which to fork (for replay context)</param>
    /// <returns>Builder with fluent methods to add additional steps</returns>
    public static ReplayForkPlanBuilder Fork(ExecutionPlan originalPlan, int forkEventIndex = 0)
        => new ReplayForkPlanBuilder(originalPlan, forkEventIndex);
}
