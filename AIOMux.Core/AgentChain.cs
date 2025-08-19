using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Core;

/// <summary>
/// Represents a chain of agents for sequential or parallel execution.
/// </summary>
public class AgentChain : IAgent
{
    private readonly List<IAgent> _chain = [];

    /// <summary>
    /// Gets or sets the name of the agent chain.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the list of agent names in the chain.
    /// </summary>
    public List<string> Agents { get; set; }

    /// <summary>
    /// Gets or sets the execution mode (sequential/parallel).
    /// </summary>
    public string Mode { get; set; }

    /// <summary>
    /// Creates a new agent chain with the specified name.
    /// </summary>
    /// <param name="name">Unique name for this chain</param>
    public AgentChain(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Adds an agent to the end of the chain.
    /// </summary>
    /// <param name="agent">Agent to add to the chain</param>
    /// <returns>This chain instance for fluent API usage</returns>
    public AgentChain AddAgent(IAgent agent)
    {
        _chain.Add(agent);
        return this;
    }

    /// <summary>
    /// Executes the chain of agents sequentially, passing context from one to the next.
    /// Each agent's output is stored in the context Variables dictionary under its name.
    /// </summary>
    /// <param name="context">The agent context to pass through the chain</param>
    /// <returns>Output from the final agent in the chain</returns>
    public async Task<string> ExecuteAsync(AgentContext context)
    {
        if (_chain.Count == 0)
        {
            return "Agent chain is empty.";
        }

        // If metrics are enabled, use the metrics collection path
        if (context.Options.CollectMetrics)
        {
            var (result, metrics) = await ExecuteWithMetricsInternalAsync(context);

            // Store metrics in context if collected
            if (metrics.Count > 0)
            {
                context.Variables["ChainMetrics"] = metrics;
            }

            return result;
        }

        // Standard execution path without metrics
        string lastResult = string.Empty;

        foreach (var agent in _chain)
        {
            // Execute current agent
            lastResult = await agent.ExecuteAsync(context);

            // Store result in context for next agent
            context.Variables[agent.Name] = lastResult;
        }

        return lastResult;
    }

    /// <summary>
    /// Internal implementation of chain execution with metrics collection
    /// </summary>
    private async Task<(string Result, List<AgentMetrics> Metrics)> ExecuteWithMetricsInternalAsync(AgentContext context)
    {
        string lastResult = string.Empty;
        var allMetrics = new List<AgentMetrics>();

        foreach (var agent in _chain)
        {
            // Execute agent with metrics collection
            var (result, metrics) = await agent.ExecuteWithMetricsAsync(context, context.Options.CollectMetrics);

            // Store result in context
            lastResult = result;
            context.Variables[agent.Name] = result;

            // Add metrics to collection if available
            if (metrics != null)
            {
                allMetrics.Add(metrics);
            }
        }

        return (lastResult, allMetrics);
    }
}