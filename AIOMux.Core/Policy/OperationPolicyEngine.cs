using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Operation and target-aware policy engine.
/// </summary>
public sealed class OperationPolicyEngine : IPolicyEngine
{
    /// <summary>
    /// Operations that are considered dangerous and require explicit constraints to be allowed.
    /// When a tool requests one of these operations and no per-operation constraints are configured,
    /// the engine denies the call.
    /// </summary>
    public static readonly IReadOnlySet<ToolOperation> DangerousOperations = new HashSet<ToolOperation>
    {
        ToolOperation.Delete,
        ToolOperation.Write,
        ToolOperation.HttpPost,
        ToolOperation.CommandExecute,
        ToolOperation.ProcessKill,
        ToolOperation.ProcessStart,
        ToolOperation.ServiceStop,
        ToolOperation.ServiceRestart,
        ToolOperation.RegistryWrite,
        ToolOperation.SecretRead
    };

    private readonly IReadOnlyDictionary<string, ToolPolicyDefinition> _policies;
    private readonly IReadOnlyDictionary<ToolTargetKind, IToolConstraintEvaluator> _evaluators;
    private readonly string _policyHash;

    /// <param name="policies">Map of tool name to operation and target constraints.</param>
    /// <param name="policyHash">Stable identifier for this policy configuration.</param>
    /// <param name="evaluators">Constraint evaluators by target kind.</param>
    public OperationPolicyEngine(
        IReadOnlyDictionary<string, ToolPolicyDefinition> policies,
        string policyHash = "operation-policy-v1",
        IReadOnlyCollection<IToolConstraintEvaluator>? evaluators = null)
    {
        _policyHash = policyHash;
        _policies = new Dictionary<string, ToolPolicyDefinition>(policies, StringComparer.OrdinalIgnoreCase);

        var registered = evaluators ??
        [
            new FilePathConstraintEvaluator(),
            new UrlConstraintEvaluator(),
            new CommandConstraintEvaluator()
        ];

        _evaluators = registered.ToDictionary(e => e.TargetKind, e => e);
    }

    /// <summary>
    /// Backward-compatible constructor for operation-only permission maps.
    /// </summary>
    public OperationPolicyEngine(
        IReadOnlyDictionary<string, IReadOnlyCollection<ToolOperation>> permissions,
        string policyHash = "operation-policy-v1")
        : this(
            permissions.ToDictionary(
                kvp => kvp.Key,
                kvp => new ToolPolicyDefinition(kvp.Value, new Dictionary<ToolOperation, ToolPolicyConstraints>()),
                StringComparer.OrdinalIgnoreCase),
            policyHash)
    {
    }

    /// <summary>
    /// Constructs an <see cref="OperationPolicyEngine"/> from a deserialized <see cref="OperationPolicyDocument"/>.
    /// The document's SHA-256 hash is used as the policy hash for audit records.
    /// </summary>
    public static OperationPolicyEngine FromDocument(OperationPolicyDocument document) =>
        new(document.ToPolicyDefinitions(), document.ComputeHash());

    public string PolicyType => "operationpolicy";

    public PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context)
    {
        if (!analysis.IsRecognized)
            return PolicyDecision.Deny("Invocation could not be analyzed", _policyHash);

        if (analysis.RequestedOperations.Count == 0)
            return PolicyDecision.Deny($"Tool '{call.ToolName}' analysis returned no operations", _policyHash);

        if (!_policies.TryGetValue(call.ToolName, out var policy))
            return PolicyDecision.Deny($"No permissions configured for tool '{call.ToolName}'", _policyHash);

        var allowed = policy.AllowedOperations.ToHashSet();
        var denied = analysis.RequestedOperations
            .Where(op => !allowed.Contains(op))
            .ToList();

        if (denied.Count == 1)
            return PolicyDecision.Deny(
                $"Operation {denied[0]} is not allowed for tool '{call.ToolName}'", _policyHash);

        if (denied.Count > 1)
            return PolicyDecision.Deny(
                $"Tool '{call.ToolName}' requested denied operations: {string.Join(", ", denied)}", _policyHash);

        var constraintsByOperation = policy.Constraints;
        foreach (var operation in analysis.RequestedOperations)
        {
            if (DangerousOperations.Contains(operation) &&
                (!constraintsByOperation.TryGetValue(operation, out var dangerConstraints) || !dangerConstraints.HasAnyConstraint))
            {
                return PolicyDecision.Deny(
                    $"Dangerous operation '{operation}' for tool '{call.ToolName}' requires explicit constraints but none are configured.",
                    _policyHash);
            }

            if (!constraintsByOperation.TryGetValue(operation, out var constraints) || !constraints.HasAnyConstraint)
                continue;

            var targets = analysis.Targets;
            if (targets.Count == 0)
                return PolicyDecision.Deny($"Tool '{call.ToolName}' operation '{operation}' is missing required targets.", _policyHash);

            foreach (var target in targets)
            {
                if (target.Kind == ToolTargetKind.None)
                    return PolicyDecision.Deny("Unknown target kind 'None' is denied.", _policyHash);

                if (!_evaluators.TryGetValue(target.Kind, out var evaluator))
                    return PolicyDecision.Deny($"Missing constraint evaluator for target kind '{target.Kind}'.", _policyHash);

                var decision = evaluator.Evaluate(target, operation, constraints);
                if (!decision.Allowed)
                    return PolicyDecision.Deny(decision.DenyReason ?? "Target constraints denied the invocation.", _policyHash);
            }
        }

        return PolicyDecision.Allow(_policyHash);
    }
}
