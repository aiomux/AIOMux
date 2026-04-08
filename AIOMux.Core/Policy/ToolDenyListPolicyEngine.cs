using System.Text.Json;

namespace AIOMux.Core.Policy;

/// <summary>
/// Policy engine that denies execution of configured tools.
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

    public PolicyDecision EvaluateStep(
        ExecutionStepMetadata stepMetadata,
        IReadOnlyDictionary<string, object?> resolvedInputs,
        ExecutionContext context)
    {
        if (!string.Equals(stepMetadata.Type, "tool", StringComparison.OrdinalIgnoreCase))
            return PolicyDecision.Allow(_policyHash);

        if (_deniedTools.Contains(stepMetadata.Target))
            return PolicyDecision.Deny($"Policy denied tool: {stepMetadata.Target}", _policyHash);

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
