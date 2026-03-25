using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

/// <summary>
/// Runtime event emitted when a tool call is proposed.
/// </summary>
public class ToolProposedEvent : RuntimeEvent
{
    public ToolProposedEvent()
    {
        Type = "ToolProposed";
    }

    /// <summary>
    /// Payload for a tool-proposed event.
    /// </summary>
    public class ToolProposedPayload
    {
        /// <summary>
        /// Identifier of the tool call.
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the proposed tool.
        /// </summary>
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Serialized arguments for the tool call.
        /// </summary>
        public string JsonArgs { get; set; } = string.Empty;
    }
}

