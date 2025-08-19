namespace AIOMux.Core.Models;

/// <summary>
/// Options for controlling agent execution behavior.
/// </summary>
public class ExecutionOptions
{
    /// <summary>
    /// Whether to collect metrics during execution. Default is true.
    /// </summary>
    public bool CollectMetrics { get; set; } = true;

    /// <summary>
    /// Whether to generate a summary report of the job execution. Default is true.
    /// </summary>
    public bool GenerateJobSummary { get; set; } = true;

    /// <summary>
    /// Whether to include detailed agent metrics in the job summary. Default is true.
    /// </summary>
    public bool IncludeDetailedMetrics { get; set; } = true;
}