namespace AIOMux.Core.Models;

/// <summary>
/// Base class for all runtime events.
/// </summary>
public abstract class RuntimeEvent
{
    /// <summary>
    /// Unique identifier for this event.
    /// </summary>
    public string EventId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID of the run this event belongs to.
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// Sequence number within the run (0-indexed).
    /// </summary>
    public int Seq { get; set; }

    /// <summary>
    /// Event type discriminator.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// When this event occurred.
    /// </summary>
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Hash of the payload for integrity verification.
    /// </summary>
    public string? PayloadHash { get; set; }

    /// <summary>
    /// Event-specific data (serialized as JSON).
    /// </summary>
    public object? Payload { get; set; }
}

/// <summary>
/// Event indicating a run has started.
/// </summary>
public class RunStartedEvent : RuntimeEvent
{
    public RunStartedEvent()
    {
        Type = "RunStarted";
    }

    public class RunStartedPayload
    {
        public string PipelineName { get; set; } = string.Empty;
        public string? PipelineVersion { get; set; }
        public string? ModelConfigHash { get; set; }
        public Dictionary<string, string> PermissionsSnapshot { get; set; } = new();
        public string? WorkingDirectory { get; set; }
    }
}

/// <summary>
/// Event indicating input was received.
/// </summary>
public class InputReceivedEvent : RuntimeEvent
{
    public InputReceivedEvent()
    {
        Type = "InputReceived";
    }

    public class InputReceivedPayload
    {
        public string Input { get; set; } = string.Empty;
        public string InputHash { get; set; } = string.Empty;
    }
}

/// <summary>
/// Event indicating a pipeline step completed.
/// </summary>
public class StepCompletedEvent : RuntimeEvent
{
    public StepCompletedEvent()
    {
        Type = "StepCompleted";
    }

    public class StepCompletedPayload
    {
        public string RunId { get; set; } = string.Empty;
        public int StepIndex { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string StepName { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public string OutputHash { get; set; } = string.Empty;
        public double DurationMs { get; set; }
    }
}
/// <summary>
/// Event indicating a step started.
/// </summary>
public class StepStartedEvent : RuntimeEvent
{
    public StepStartedEvent()
    {
        Type = "StepStarted";
    }

    public class StepStartedPayload
    {
        public string RunId { get; set; } = string.Empty;
        public int StepIndex { get; set; }
        public string AgentName { get; set; } = string.Empty;
    }
}

/// <summary>
/// Event indicating a step failed.
/// </summary>
public class StepFailedEvent : RuntimeEvent
{
    public StepFailedEvent()
    {
        Type = "StepFailed";
    }

    public class StepFailedPayload
    {
        public string RunId { get; set; } = string.Empty;
        public int StepIndex { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string ExceptionMessage { get; set; } = string.Empty;
    }
}

/// <summary>
/// Event indicating a tool was invoked.
/// </summary>
public class ToolInvokedEvent : RuntimeEvent
{
    public ToolInvokedEvent()
    {
        Type = "ToolInvoked";
    }

    public class ToolInvokedPayload
    {
        public string ToolName { get; set; } = string.Empty;
        public string Args { get; set; } = string.Empty;
        public string ArgsHash { get; set; } = string.Empty;
    }
}

/// <summary>
/// Event indicating a tool returned a result.
/// </summary>
public class ToolResultEvent : RuntimeEvent
{
    public ToolResultEvent()
    {
        Type = "ToolResult";
    }

    public class ToolResultPayload
    {
        public string ToolName { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public string ResultHash { get; set; } = string.Empty;
        public double DurationMs { get; set; }
        public bool Success { get; set; } = true;
        public string? Error { get; set; }
    }
}

/// <summary>
/// Event indicating the run finished.
/// </summary>
public class RunFinishedEvent : RuntimeEvent
{
    public RunFinishedEvent()
    {
        Type = "RunFinished";
    }

    public class RunFinishedPayload
    {
        public bool Success { get; set; }
        public string? FinalOutput { get; set; }
        public string? Error { get; set; }
        public double TotalDurationMs { get; set; }
    }
}
