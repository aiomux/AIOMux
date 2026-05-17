using AIOMux.Core.Models;

namespace AIOMux.Core.Policy;

/// <summary>
/// Enforces host, scheme, and port constraints for URL targets.
/// </summary>
public sealed class UrlConstraintEvaluator : IToolConstraintEvaluator
{
    private const string PolicyHash = "operation-policy-v1";

    public ToolTargetKind TargetKind => ToolTargetKind.Url;

    public PolicyDecision Evaluate(ToolTarget target, ToolOperation operation, ToolPolicyConstraints constraints)
    {
        if (target.Kind != ToolTargetKind.Url)
            return PolicyDecision.Deny($"Constraint evaluator mismatch for target kind '{target.Kind}'.", PolicyHash);

        if (!Uri.TryCreate(target.Value, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
            return PolicyDecision.Deny($"Malformed URL target: '{target.Value}'.", PolicyHash);

        var host = uri.Host.ToLowerInvariant();
        var scheme = uri.Scheme.ToLowerInvariant();
        var port = uri.Port;

        var deniedHosts = Normalize(constraints.DeniedHosts);
        if (deniedHosts.Contains(host))
            return PolicyDecision.Deny($"URL host '{host}' is denied.", PolicyHash);

        var allowedHosts = Normalize(constraints.AllowedHosts);
        if (allowedHosts.Count > 0 && !allowedHosts.Contains(host))
            return PolicyDecision.Deny($"URL host '{host}' is not allowed.", PolicyHash);

        var deniedSchemes = Normalize(constraints.DeniedSchemes);
        if (deniedSchemes.Contains(scheme))
            return PolicyDecision.Deny($"URL scheme '{scheme}' is denied.", PolicyHash);

        var allowedSchemes = Normalize(constraints.AllowedSchemes);
        if (allowedSchemes.Count > 0 && !allowedSchemes.Contains(scheme))
            return PolicyDecision.Deny($"URL scheme '{scheme}' is not allowed.", PolicyHash);

        if (port >= 0)
        {
            var deniedPorts = constraints.DeniedPorts;
            if (deniedPorts != null && deniedPorts.Count > 0 && deniedPorts.Contains(port))
                return PolicyDecision.Deny($"URL port '{port}' is denied.", PolicyHash);

            var allowedPorts = constraints.AllowedPorts;
            if (allowedPorts != null && allowedPorts.Count > 0 && !allowedPorts.Contains(port))
                return PolicyDecision.Deny($"URL port '{port}' is not allowed.", PolicyHash);
        }

        return PolicyDecision.Allow(PolicyHash);
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
