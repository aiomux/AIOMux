namespace AIOMux.Core.Models;

/// <summary>
/// Contains metrics about an agent's execution.
/// </summary>
public class AgentMetrics
{
    /// <summary>
    /// Name of the agent.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public double ExecutionTimeMs { get; set; }

    /// <summary>
    /// When the agent execution started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the agent execution completed.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Optional custom metrics specific to the agent type.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; set; } = new();
}