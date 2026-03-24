using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core.Models;

/// <summary>
/// Request to fork execution from a previous recorded run.
/// </summary>
public sealed record ExecutionForkRequest
{
    /// <summary>Run ID to fork from.</summary>
    public string SourceRunId { get; init; } = string.Empty;

    /// <summary>Event index (inclusive) to hydrate context up to.</summary>
    public int EventIndex { get; init; }

    /// <summary>The plan to execute after hydration.</summary>
    public ExecutionPlan Plan { get; init; } = default!;

    /// <summary>The runtime context to hydrate and run with.</summary>
    public ExecutionContext Context { get; init; } = default!;

    /// <summary>Optional policy engine applied during tool execution.</summary>
    public IPolicyEngine? PolicyEngine { get; init; }

    /// <summary>Replay mode used during execution.</summary>
    public ReplayMode ReplayMode { get; init; } = ReplayMode.None;
}
