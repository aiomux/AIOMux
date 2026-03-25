using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

/// <summary>
/// Runtime event emitted after a tool call is executed.
/// </summary>
public class ToolExecutedEvent : RuntimeEvent
{
    public ToolExecutedEvent()
    {
        Type = "ToolExecuted";
    }

    /// <summary>
    /// Payload for a tool-executed event.
    /// </summary>
    public class ToolExecutedPayload
    {
        /// <summary>
        /// Identifier of the tool call.
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the executed tool.
        /// </summary>
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the result came from replay.
        /// </summary>
        public bool FromReplay { get; set; }
    }
}

