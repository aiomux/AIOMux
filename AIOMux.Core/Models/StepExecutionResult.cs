namespace AIOMux.Core.Models;

/// <summary>
/// Result of executing a single step within a plan.
/// Includes policy evaluation information for audit trail.
/// </summary>
public sealed record StepExecutionResult
{
    /// <summary>Whether the step completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Tool targets analyzed for this step, if applicable.</summary>
    public IReadOnlyCollection<ToolTarget> ToolTargets { get; init; } = [];

    /// <summary>Primary text output produced by the step.</summary>
    public string Output { get; init; } = string.Empty;

    /// <summary>Named outputs produced by the step, keyed by output name.</summary>
    public Dictionary<string, object?> Outputs { get; init; } = new();

    /// <summary>Error message when the step fails or is denied.</summary>
    public string? Error { get; init; }

    /// <summary>Reason for policy denial, if applicable. Null if step was not denied by policy.</summary>
    public string? PolicyDenyReason { get; init; }

    /// <summary>Hash of the policy that evaluated this step, for audit trail.</summary>
    public string? PolicyHash { get; init; }

    /// <summary>Type identifier of the policy engine that evaluated this step (for example: "allowall", "tooldenylist").</summary>
    public string? PolicyType { get; init; }
}
