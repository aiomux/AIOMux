using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Evaluates whether a step is allowed to execute based on policy.
/// Policy decisions are made before step execution for both tools and agents.
/// </summary>
public interface IPolicyEngine
{
    /// <summary>
    /// Evaluates whether a step is allowed to execute.
    /// </summary>
    /// <param name="stepMetadata">Metadata about the step (type, target, id)</param>
    /// <param name="resolvedInputs">The resolved input values for the step (from bindings + static)</param>
    /// <param name="context">The current execution context</param>
    /// <returns>A decision indicating whether the step is allowed, and reason if denied</returns>
    PolicyDecision EvaluateStep(
        ExecutionStepMetadata stepMetadata,
        IReadOnlyDictionary<string, object?> resolvedInputs,
        ExecutionContext context);

    /// <summary>
    /// Evaluates whether a tool call is allowed (for backward compatibility and detailed tool policies).
    /// Used specifically for tool execution after step-level policy passes.
    /// </summary>
    /// <param name="call">The tool call details</param>
    /// <param name="context">The current execution context</param>
    /// <returns>A decision indicating whether the tool call is allowed</returns>
    PolicyDecision EvaluateToolCall(ToolCall call, ExecutionContext context) =>
        // Default implementation: if EvaluateStep passed for this tool, allow the call
        PolicyDecision.Allow();
}

/// <summary>
/// Metadata about an execution step for policy evaluation.
/// </summary>
public class ExecutionStepMetadata
{
    /// <summary>Step identifier.</summary>
    public string StepId { get; init; } = string.Empty;

    /// <summary>Step type: "tool" or "agent".</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Target name (tool name or agent name).</summary>
    public string Target { get; init; } = string.Empty;
}



