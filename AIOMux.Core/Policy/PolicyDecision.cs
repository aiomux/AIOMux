namespace AIOMux.Core.Policy;

public sealed class PolicyDecision
{
    public bool Allowed { get; init; }
    public string? DenyReason { get; init; }
    public string PolicyHash { get; init; } = string.Empty;

    public static PolicyDecision Allow(string policyHash = "allow-all") => new() { Allowed = true, PolicyHash = policyHash };
    public static PolicyDecision Deny(string reason, string policyHash) => new() { Allowed = false, DenyReason = reason, PolicyHash = policyHash };
}
