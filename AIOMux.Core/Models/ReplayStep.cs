namespace AIOMux.Core.Models;

/// <summary>
/// A step reconstructed from replay.
/// </summary>
public class ReplayStep
{
    public string StepName { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
}

