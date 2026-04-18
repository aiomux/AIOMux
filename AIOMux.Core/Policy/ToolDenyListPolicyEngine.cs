using AIOMux.Core.Models;
using System.Text.Json;

namespace AIOMux.Core.Policy;

/// <summary>
/// Policy engine that denies execution of explicitly listed tools by name.
/// All other tools are allowed regardless of requested operations.
/// </summary>
public sealed class ToolDenyListPolicyEngine : IPolicyEngine
{
    private readonly HashSet<string> _deniedTools;
    private readonly string _policyHash;

    public ToolDenyListPolicyEngine(IEnumerable<string> deniedTools, string policyHash = "tool-denylist-v1")
    {
        _deniedTools = new HashSet<string>(
            deniedTools.Where(t => !string.IsNullOrWhiteSpace(t)),
            StringComparer.OrdinalIgnoreCase);
        _policyHash = policyHash;
    }

    public PolicyDecision Evaluate(ToolCall call, ToolExecutionAnalysis analysis, AgentContext context)
    {
        if (_deniedTools.Contains(call.ToolName))
            return PolicyDecision.Deny($"Policy denied tool: {call.ToolName}", _policyHash);

        return PolicyDecision.Allow(_policyHash);
    }

    public static IEnumerable<string> ParseDeniedTools(object? rawValue)
    {
        if (rawValue is null)
            return [];

        if (rawValue is string single)
            return [single];

        if (rawValue is IEnumerable<string> direct)
            return direct;

        if (rawValue is JsonElement json && json.ValueKind == JsonValueKind.Array)
        {
            return json.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!);
        }

        return [];
    }
}
