using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Policy engine that allows all tool invocations to execute.
/// </summary>
public class AllowAllPolicyEngine : IPolicyEngine
{
    public PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context)
        => PolicyDecision.Allow();
}



