using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract every agent must implement (SRP, DIP).
/// </summary>
public interface IAgent
{
    /// <summary> Unique name used for lookup. </summary>
    string Name { get; }

    /// <summary>
    /// Execute the agent with the given context and return a result string.
    /// </summary>
    /// <param name="context">The context containing input and execution options</param>
    /// <returns>The result of agent execution</returns>
    Task<string> ExecuteAsync(AgentContext context);

    /// <summary>
    /// Execute the agent with the given context and optionally collect metrics.
    /// </summary>
    /// <param name="context">The context containing input and execution options</param>
    /// <param name="collectMetrics">Whether to collect metrics during execution</param>
    /// <returns>A tuple containing the execution result and optional metrics</returns>
    async Task<(string Result, AgentMetrics? Metrics)> ExecuteWithMetricsAsync(AgentContext context, bool collectMetrics = true)
    {
        var startTime = DateTime.UtcNow;
        var result = await ExecuteAsync(context);
        var endTime = DateTime.UtcNow;

        if (collectMetrics)
        {
            return (result, new AgentMetrics
            {
                AgentName = Name,
                ExecutionTimeMs = (endTime - startTime).TotalMilliseconds,
                StartTime = startTime,
                EndTime = endTime
            });
        }

        return (result, null);
    }
}