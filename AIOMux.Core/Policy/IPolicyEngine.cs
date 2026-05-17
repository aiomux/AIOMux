using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Evaluates whether a tool invocation is allowed to execute based on policy.
/// Called by the runtime after <c>ITool.Analyze</c> and before <c>ITool.ExecuteAsync</c>.
/// Agent steps are not subject to policy evaluation.
/// </summary>
public interface IPolicyEngine
{
    /// <summary>
    /// Evaluates a tool invocation against the active policy.
    /// </summary>
    /// <param name="call">The tool and input being invoked.</param>
    /// <param name="analysis">
    /// Operation analysis produced by <c>ITool.Analyze</c>.
    /// For tools not present in the registry a synthetic unrecognized analysis is supplied.
    /// </param>
    /// <param name="context">Minimal execution context for the current run.</param>
    /// <returns>A <see cref="PolicyDecision"/> indicating allow or deny with reason.</returns>
    PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context);

    /// <summary>
    /// Identifies the policy engine type for audit trail records (for example: "allowall", "tooldenylist").
    /// </summary>
    string PolicyType { get; }
}



