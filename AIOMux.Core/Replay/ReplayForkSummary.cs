namespace AIOMux.Core.Replay;

/// <summary>
/// Simple replay/fork summary derived from execution records only.
/// </summary>
public sealed class ReplayForkSummary
{
    public string SourceRunId { get; set; } = string.Empty;

    public int ForkStepIndex { get; set; }

    public int ReconstructedRecordCount { get; set; }

    public int LastReconstructedStepIndex { get; set; }

    public int SuccessfulStepCount { get; set; }

    public int FailedStepCount { get; set; }

    public int PolicyDeniedStepCount { get; set; }

    public int ReplayedToolCallCount { get; set; }

    public double TotalDurationMs { get; set; }

    public List<ReplayStepSummary> Steps { get; set; } = [];

    public List<ReplayToolCallSummary> ToolCalls { get; set; } = [];
}

/// <summary>
/// Per-step replay summary derived from <see cref="AIOMux.Core.Models.ExecutionRecord"/>.
/// </summary>
public sealed class ReplayStepSummary
{
    public string StepId { get; set; } = string.Empty;

    public int StepIndex { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public bool Success { get; set; }

    public double DurationMs { get; set; }

    public string? Error { get; set; }

    public string? PolicyDenyReason { get; set; }
}

/// <summary>
/// Per-tool-call replay summary derived from <see cref="AIOMux.Core.Models.ExecutionRecord"/>.
/// </summary>
public sealed class ReplayToolCallSummary
{
    public string StepId { get; set; } = string.Empty;

    public int StepIndex { get; set; }

    public string ToolName { get; set; } = string.Empty;

    public string ReplayKey { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string Input { get; set; } = string.Empty;

    public string? Output { get; set; }

    public string? Error { get; set; }
}
