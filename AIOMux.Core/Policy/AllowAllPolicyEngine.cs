using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Policy engine that allows all steps and tool calls to execute.
/// </summary>
public class AllowAllPolicyEngine : IPolicyEngine
{
    public PolicyDecision EvaluateStep(
        ExecutionStepMetadata stepMetadata,
        IReadOnlyDictionary<string, object?> resolvedInputs,
        ExecutionContext context)
        => PolicyDecision.Allow();

    public PolicyDecision EvaluateToolCall(ToolCall call, ExecutionContext context)
        => PolicyDecision.Allow();
}



