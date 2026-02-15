using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

public class ToolProposedEvent : RuntimeEvent
{
    public ToolProposedEvent()
    {
        Type = "ToolProposed";
    }

    public class ToolProposedPayload
    {
        public string CallId { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public string JsonArgs { get; set; } = string.Empty;
    }
}
