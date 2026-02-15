using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

public class ToolExecutedEvent : RuntimeEvent
{
    public ToolExecutedEvent()
    {
        Type = "ToolExecuted";
    }

    public class ToolExecutedPayload
    {
        public string CallId { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public bool FromReplay { get; set; }
    }
}
