using System.Text;

namespace AIOMux.Core.Models;

/// <summary>
/// Provides a summary of a completed job execution.
/// </summary>
public class JobSummary
{
    /// <summary>
    /// Name of the job or chain that was executed.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Total execution time in milliseconds.
    /// </summary>
    public double TotalExecutionTimeMs { get; set; }

    /// <summary>
    /// When the job started.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// When the job completed.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Number of agents that were executed.
    /// </summary>
    public int AgentCount { get; set; }

    /// <summary>
    /// List of agent metrics for each agent in the chain.
    /// </summary>
    public List<AgentMetrics> AgentMetrics { get; set; } = new();

    /// <summary>
    /// Creates a formatted string report of the job summary.
    /// </summary>
    /// <param name="includeDetailedMetrics">Whether to include detailed agent metrics</param>
    /// <returns>A formatted report string</returns>
    public string CreateReport(bool includeDetailedMetrics = true)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"--- Job Summary: {Name} ---");
        sb.AppendLine($"Start Time: {StartTime:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"End Time: {EndTime:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Total Execution Time: {TotalExecutionTimeMs:F2} ms");
        sb.AppendLine($"Agents Executed: {AgentCount}");

        if (includeDetailedMetrics && AgentMetrics.Count > 0)
        {
            sb.AppendLine("\nDetailed Agent Metrics:");
            foreach (var metrics in AgentMetrics)
            {
                sb.AppendLine($"  - {metrics.AgentName}: {metrics.ExecutionTimeMs:F2} ms");

                if (metrics.CustomMetrics.Count > 0)
                {
                    foreach (var customMetric in metrics.CustomMetrics)
                    {
                        sb.AppendLine($"    * {customMetric.Key}: {customMetric.Value}");
                    }
                }
            }
        }

        return sb.ToString();
    }
}