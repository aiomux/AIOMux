using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Enforces command name and argument constraints for command targets.
/// </summary>
public sealed class CommandConstraintEvaluator : IToolConstraintEvaluator
{
    private const string PolicyHash = "operation-policy-v1";

    public ToolTargetKind TargetKind => ToolTargetKind.Command;

    public PolicyDecision Evaluate(ToolTarget target, ToolOperation operation, ToolPolicyConstraints constraints)
    {
        if (target.Kind != ToolTargetKind.Command)
            return PolicyDecision.Deny($"Constraint evaluator mismatch for target kind '{target.Kind}'.", PolicyHash);

        if (string.IsNullOrWhiteSpace(target.Value))
            return PolicyDecision.Deny("Malformed command target: value is empty.", PolicyHash);

        var command = NormalizeCommand(target.Value);
        if (string.IsNullOrWhiteSpace(command))
            return PolicyDecision.Deny($"Malformed command target: '{target.Value}'.", PolicyHash);

        var denied = Normalize(constraints.DeniedCommands);
        if (denied.Contains(command))
            return PolicyDecision.Deny($"Command target '{command}' is denied.", PolicyHash);

        var allowed = Normalize(constraints.AllowedCommands);
        if (allowed.Count > 0 && !allowed.Contains(command))
            return PolicyDecision.Deny($"Command target '{command}' is not allowed.", PolicyHash);

        var deniedArguments = constraints.DeniedArguments;
        if (deniedArguments != null && deniedArguments.Count > 0)
        {
            var rawArgs = ExtractArguments(target.Value);
            foreach (var deniedArg in deniedArguments)
            {
                if (string.IsNullOrWhiteSpace(deniedArg))
                    continue;

                if (rawArgs.Any(a => a.Equals(deniedArg.Trim(), StringComparison.OrdinalIgnoreCase)))
                    return PolicyDecision.Deny($"Command argument '{deniedArg.Trim()}' is denied.", PolicyHash);
            }
        }

        return PolicyDecision.Allow(PolicyHash);
    }

    private static string NormalizeCommand(string value)
    {
        var trimmed = value.Trim();
        var token = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return token.ToLowerInvariant();
    }

    private static string[] ExtractArguments(string value)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1..] : [];
    }

    private static HashSet<string> Normalize(IReadOnlyCollection<string>? values)
    {
        if (values == null || values.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
