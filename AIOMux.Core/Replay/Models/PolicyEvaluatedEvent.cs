using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

/// <summary>
/// Runtime event emitted after policy evaluation for a tool call.
/// </summary>
public class PolicyEvaluatedEvent : RuntimeEvent
{
    public PolicyEvaluatedEvent()
    {
        Type = "PolicyEvaluated";
    }

    /// <summary>
    /// Payload for a policy-evaluated event.
    /// </summary>
    public class PolicyEvaluatedPayload
    {
        /// <summary>
        /// Identifier of the tool call.
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the evaluated tool.
        /// </summary>
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether execution is allowed.
        /// </summary>
        public bool Allowed { get; set; }

        /// <summary>
        /// Reason for deny decisions.
        /// </summary>
        public string? DenyReason { get; set; }

        /// <summary>
        /// Hash of the evaluated policy.
        /// </summary>
        public string PolicyHash { get; set; } = string.Empty;
    }
}

