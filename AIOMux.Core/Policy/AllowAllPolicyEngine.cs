namespace AIOMux.Core.Policy;

/// <summary>
/// Policy engine that allows all steps to execute.
/// </summary>
public class AllowAllPolicyEngine : IPolicyEngine
{
    public PolicyDecision EvaluateStep(
        ExecutionStepMetadata stepMetadata,
        IReadOnlyDictionary<string, object?> resolvedInputs,
        ExecutionContext context)
        => PolicyDecision.Allow();
}



