namespace AIOMux.Core.Models;

/// <summary>
/// Result of replaying a run.
/// </summary>
public class ReplayResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? RunId { get; set; }
    public string? PipelineName { get; set; }
    public string? Input { get; set; }
    public string? FinalOutput { get; set; }
    public double TotalDurationMs { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<RuntimeEvent> Events { get; set; } = new();
    public List<ReplayStep> Steps { get; set; } = new();
    public List<ReplayToolCall> ToolCalls { get; set; } = new();
}

