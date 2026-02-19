using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Core
{
    public static class ToolExecution
    {
        public static async Task<string> RunAsync(IAgent agent, AgentContext context, CancellationToken ct = default)
        {
            if (agent is ITool tool)
            {
                var result = await context.ExecuteToolAsync(tool.Name, context.UserInput, ct);
                return result.JsonResult;
            }
            return await agent.ExecuteAsync(context);
        }

        public static async Task<(string Result, AgentMetrics? Metrics)> RunWithMetricsAsync(IAgent agent, AgentContext context, CancellationToken ct = default)
        {
            if (agent is ITool tool)
            {
                var result = await context.ExecuteToolAsync(tool.Name, context.UserInput, ct);
                // No metrics for tool, but could be extended
                return (result.JsonResult, null);
            }
            return await agent.ExecuteWithMetricsAsync(context);
        }
    }
}


