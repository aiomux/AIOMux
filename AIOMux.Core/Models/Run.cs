namespace AIOMux.Core.Models;

/// <summary>
/// Represents a single execution run of a plan.
/// </summary>
public class Run
{
    /// <summary>Unique identifier for this run.</summary>
    public string RunId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Name of the plan being executed.</summary>
    public string PipelineName { get; set; } = string.Empty;

    /// <summary>When the run started.</summary>
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>When the run completed (null if still running).</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>Whether the run completed successfully.</summary>
    public bool? Success { get; set; }

    /// <summary>Working directory at execution time.</summary>
    public string? WorkingDirectory { get; set; }
}
