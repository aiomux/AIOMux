using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Enforces path-based allow/deny constraints for file targets.
/// </summary>
public sealed class FilePathConstraintEvaluator : IToolConstraintEvaluator
{
    private const string PolicyHash = "operation-policy-v1";

    public ToolTargetKind TargetKind => ToolTargetKind.FilePath;

    public PolicyDecision Evaluate(ToolTarget target, ToolOperation operation, ToolPolicyConstraints constraints)
    {
        if (target.Kind != ToolTargetKind.FilePath)
            return PolicyDecision.Deny($"Constraint evaluator mismatch for target kind '{target.Kind}'.", PolicyHash);

        if (string.IsNullOrWhiteSpace(target.Value))
            return PolicyDecision.Deny("Malformed file path target: value is empty.", PolicyHash);

        if (!TryNormalizePath(target.Value, out var normalizedTarget))
            return PolicyDecision.Deny($"Malformed file path target: '{target.Value}'.", PolicyHash);

        var denied = NormalizeMany(constraints.DeniedPaths);
        if (denied.Any(prefix => IsPathWithin(normalizedTarget, prefix)))
            return PolicyDecision.Deny($"File path target '{target.Value}' is denied.", PolicyHash);

        var allowed = NormalizeMany(constraints.AllowedPaths);
        if (allowed.Count > 0 && !allowed.Any(prefix => IsPathWithin(normalizedTarget, prefix)))
            return PolicyDecision.Deny($"File path target '{target.Value}' is outside allowed paths.", PolicyHash);

        return PolicyDecision.Allow(PolicyHash);
    }

    private static bool TryNormalizePath(string value, out string normalized)
    {
        try
        {
            normalized = Path.GetFullPath(value)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return !string.IsNullOrWhiteSpace(normalized);
        }
        catch
        {
            normalized = string.Empty;
            return false;
        }
    }

    private static List<string> NormalizeMany(IReadOnlyCollection<string>? raw)
    {
        if (raw == null || raw.Count == 0)
            return [];

        var normalized = new List<string>();
        foreach (var value in raw)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (TryNormalizePath(value, out var full))
                normalized.Add(full);
        }

        return normalized;
    }

    private static bool IsPathWithin(string candidate, string root)
    {
        if (candidate.Equals(root, StringComparison.OrdinalIgnoreCase))
            return true;

        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}
