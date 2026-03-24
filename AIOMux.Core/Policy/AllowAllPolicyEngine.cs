using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

public class AllowAllPolicyEngine : IPolicyEngine
{
    public PolicyDecision Evaluate(ToolCall call, ExecutionContext context)
        => PolicyDecision.Allow();
}



