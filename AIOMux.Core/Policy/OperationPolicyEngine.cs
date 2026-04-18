using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Operation-based policy engine. Each tool has an explicit set of allowed operations;
/// everything else is implicitly denied.
///
/// Deny-by-default rules evaluated in order:
///   1. analysis.IsRecognized == false  =>  "Invocation could not be analyzed"
///   2. RequestedOperations is empty    =>  "Tool '{name}' analysis returned no operations"
///   3. Tool not in permissions map     =>  "No permissions configured for tool '{name}'"
///   4. Any requested op outside the allowed set  =>  per-operation deny reason
/// </summary>
public sealed class OperationPolicyEngine : IPolicyEngine
{
    private readonly IReadOnlyDictionary<string, IReadOnlySet<ToolOperation>> _permissions;
    private readonly string _policyHash;

    /// <param name="permissions">Map of tool name to the set of operations it is allowed to perform.</param>
    /// <param name="policyHash">Stable identifier for this policy configuration.</param>
    public OperationPolicyEngine(
        IReadOnlyDictionary<string, IReadOnlyCollection<ToolOperation>> permissions,
        string policyHash = "operation-policy-v1")
    {
        _policyHash = policyHash;
        _permissions = permissions.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlySet<ToolOperation>)kvp.Value.ToHashSet(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Constructs an <see cref="OperationPolicyEngine"/> from a deserialized <see cref="OperationPolicyDocument"/>.
    /// The document's SHA-256 hash is used as the policy hash for audit records.
    /// </summary>
    public static OperationPolicyEngine FromDocument(OperationPolicyDocument document) =>
        new(document.ToPermissions(), document.ComputeHash());

    public PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context)
    {
        if (!analysis.IsRecognized)
            return PolicyDecision.Deny("Invocation could not be analyzed", _policyHash);

        if (analysis.RequestedOperations.Count == 0)
            return PolicyDecision.Deny($"Tool '{call.ToolName}' analysis returned no operations", _policyHash);

        if (!_permissions.TryGetValue(call.ToolName, out var allowed))
            return PolicyDecision.Deny($"No permissions configured for tool '{call.ToolName}'", _policyHash);

        var denied = analysis.RequestedOperations
            .Where(op => !allowed.Contains(op))
            .ToList();

        if (denied.Count == 1)
            return PolicyDecision.Deny(
                $"Operation {denied[0]} is not allowed for tool '{call.ToolName}'", _policyHash);

        if (denied.Count > 1)
            return PolicyDecision.Deny(
                $"Tool '{call.ToolName}' requested denied operations: {string.Join(", ", denied)}", _policyHash);

        return PolicyDecision.Allow(_policyHash);
    }
}
