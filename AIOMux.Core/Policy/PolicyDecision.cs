namespace AIOMux.Core.Policy;

/// <summary>
/// Represents the result of a policy evaluation.
/// </summary>
public sealed class PolicyDecision
{
    /// <summary>
    /// Gets a value indicating whether execution is allowed.
    /// </summary>
    public bool Allowed { get; init; }

    /// <summary>
    /// Gets the reason for a deny decision.
    /// </summary>
    public string? DenyReason { get; init; }

    /// <summary>
    /// Gets the policy hash used during evaluation.
    /// </summary>
    public string PolicyHash { get; init; } = string.Empty;

    /// <summary>
    /// Creates an allow decision.
    /// </summary>
    public static PolicyDecision Allow(string policyHash = "allow-all") => new() { Allowed = true, PolicyHash = policyHash };

    /// <summary>
    /// Creates a deny decision.
    /// </summary>
    public static PolicyDecision Deny(string reason, string policyHash) => new() { Allowed = false, DenyReason = reason, PolicyHash = policyHash };
}
