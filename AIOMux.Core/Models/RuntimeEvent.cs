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

    /// <summary>
    /// Payload for a run-started event.
    /// </summary>
    public class RunStartedPayload
    {
        /// <summary>
        /// Name of the pipeline that started.
        /// </summary>
        public string PipelineName { get; set; } = string.Empty;

        /// <summary>
        /// Working directory for the run.
        /// </summary>
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

    /// <summary>
    /// Payload for an input-received event.
    /// </summary>
    public class InputReceivedPayload
    {
        /// <summary>
        /// Input value received by the runtime.
        /// </summary>
        public string Input { get; set; } = string.Empty;

        /// <summary>
        /// Hash of the input value.
        /// </summary>
        public string InputHash { get; set; } = string.Empty;
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

    /// <summary>
    /// Payload for a step-started event.
    /// </summary>
    public class StepStartedPayload
    {
        /// <summary>
        /// Identifier of the run.
        /// </summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>
        /// Zero-based step index.
        /// </summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// Target executed by the step.
        /// </summary>
        public string StepTarget { get; set; } = string.Empty;
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

    /// <summary>
    /// Payload for a step-completed event.
    /// </summary>
    public class StepCompletedPayload
    {
        /// <summary>
        /// Identifier of the run.
        /// </summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>
        /// Zero-based step index.
        /// </summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// Target executed by the step.
        /// </summary>
        public string StepTarget { get; set; } = string.Empty;

        /// <summary>
        /// Name of the step.
        /// </summary>
        public string StepName { get; set; } = string.Empty;

        /// <summary>
        /// Output produced by the step.
        /// </summary>
        public string Output { get; set; } = string.Empty;

        /// <summary>
        /// Hash of the output.
        /// </summary>
        public string OutputHash { get; set; } = string.Empty;

        /// <summary>
        /// Step duration in milliseconds.
        /// </summary>
        public double DurationMs { get; set; }
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

    /// <summary>
    /// Payload for a step-failed event.
    /// </summary>
    public class StepFailedPayload
    {
        /// <summary>
        /// Identifier of the run.
        /// </summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>
        /// Zero-based step index.
        /// </summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// Target executed by the step.
        /// </summary>
        public string StepTarget { get; set; } = string.Empty;

        /// <summary>
        /// Error message from the step failure.
        /// </summary>
        public string ExceptionMessage { get; set; } = string.Empty;
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

    /// <summary>
    /// Payload for a run-finished event.
    /// </summary>
    public class RunFinishedPayload
    {
        /// <summary>
        /// Indicates whether the run completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Final output produced by the run.
        /// </summary>
        public string? FinalOutput { get; set; }

        /// <summary>
        /// Error message when the run fails.
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Total run duration in milliseconds.
        /// </summary>
        public double TotalDurationMs { get; set; }
    }
}

