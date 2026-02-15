namespace AIOMux.Core.Models;

/// <summary>
/// Represents a single execution run of an agent or pipeline.
/// </summary>
public class Run
{
    /// <summary>
    /// Unique identifier for this run.
    /// </summary>
    public string RunId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the agent or pipeline being executed.
    /// </summary>
    public string PipelineName { get; set; } = string.Empty;

    /// <summary>
    /// Version of the pipeline (if available).
    /// </summary>
    public string? PipelineVersion { get; set; }

    /// <summary>
    /// Hash of the model configuration used for this run.
    /// </summary>
    public string? ModelConfigHash { get; set; }

    /// <summary>
    /// Snapshot of permissions at the time of execution.
    /// </summary>
    public Dictionary<string, string> PermissionsSnapshot { get; set; } = new();

    /// <summary>
    /// When the run started.
    /// </summary>
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the run completed (null if still running).
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Whether the run completed successfully.
    /// </summary>
    public bool? Success { get; set; }

    /// <summary>
    /// Working directory at execution time.
    /// </summary>
    public string? WorkingDirectory { get; set; }
}
