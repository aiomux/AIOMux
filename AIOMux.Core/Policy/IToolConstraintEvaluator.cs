using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Evaluates whether a concrete tool target satisfies configured operation constraints.
/// </summary>
public interface IToolConstraintEvaluator
{
    /// <summary>
    /// Target kind this evaluator supports.
    /// </summary>
    ToolTargetKind TargetKind { get; }

    /// <summary>
    /// Evaluates one target against the configured constraints for a specific operation.
    /// </summary>
    PolicyDecision Evaluate(
        ToolTarget target,
        ToolOperation operation,
        ToolPolicyConstraints constraints);
}
