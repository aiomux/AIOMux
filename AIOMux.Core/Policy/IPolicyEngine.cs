using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

public interface IPolicyEngine
{
    PolicyDecision Evaluate(ToolCall call, ExecutionContext context);
}



