namespace AIOMux.Core.Models;

/// <summary>
/// Represents the result of an agent's execution including both output and metrics.
/// </summary>
public class AgentResult
{
    /// <summary>
    /// The string output from the agent execution.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// The metrics collected during agent execution.
    /// </summary>
    public AgentMetrics Metrics { get; set; } = new AgentMetrics();
}