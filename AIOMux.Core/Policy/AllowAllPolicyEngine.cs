using AIOMux.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIOMux.Core.Policy;

/// <summary>
/// Policy engine that allows all tool invocations to execute.
/// Intended for development and testing only. Never use in production.
/// </summary>
public class AllowAllPolicyEngine : IPolicyEngine
{
    private readonly ILogger? _logger;

    /// <summary>
    /// Creates an AllowAllPolicyEngine and emits startup warnings.
    /// </summary>
    /// <param name="logger">Optional logger for warning output.</param>
    public AllowAllPolicyEngine(ILogger? logger = null)
    {
        _logger = logger;
        _logger?.LogWarning("WARNING: AllowAllPolicyEngine is enabled.");
        _logger?.LogWarning("All tool operations will be permitted.");
        _logger?.LogWarning("This configuration is unsafe for production use.");
    }

    public string PolicyType => "allowall";

    public PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context)
        => PolicyDecision.Allow();
}



