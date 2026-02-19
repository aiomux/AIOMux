namespace AIOMux.Core.Models;

/// <summary>
/// A tool call reconstructed from replay.
/// </summary>
public class ReplayToolCall
{
    public string CallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
    public string? Result { get; set; }
    public double DurationMs { get; set; }
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }
    public bool? PolicyAllowed { get; set; }
    public string? PolicyDenyReason { get; set; }
    public bool FromReplay { get; set; }
}

