using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

public class PolicyEvaluatedEvent : RuntimeEvent
{
    public PolicyEvaluatedEvent()
    {
        Type = "PolicyEvaluated";
    }

    public class PolicyEvaluatedPayload
    {
        public string CallId { get; set; } = string.Empty;
        public string ToolName { get; set; } = string.Empty;
        public bool Allowed { get; set; }
        public string? DenyReason { get; set; }
        public string PolicyHash { get; set; } = string.Empty;
    }
}

