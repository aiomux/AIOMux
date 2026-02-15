using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core.Models;

/// <summary>
/// Request to fork execution from a previous run and continue.
/// </summary>
public sealed record AgentForkRequest
{
    /// <summary>
    /// The run ID to fork from.
    /// </summary>
    public string SourceRunId { get; init; } = string.Empty;

    /// <summary>
    /// Event index to hydrate up to (inclusive).
    /// </summary>
    public int EventIndex { get; init; }

    /// <summary>
    /// Name of a specific agent to execute (mutually exclusive with ChainName).
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// Name of a chain to execute (mutually exclusive with AgentName).
    /// </summary>
    public string? ChainName { get; init; }

    /// <summary>
    /// The execution context (must not be null).
    /// </summary>
    public AgentContext Context { get; init; } = default!;

    /// <summary>
    /// Optional policy engine to apply during tool execution.
    /// </summary>
    public IPolicyEngine? PolicyEngine { get; init; }

    /// <summary>
    /// Replay mode to use during execution.
    /// </summary>
    public ReplayMode ReplayMode { get; init; } = ReplayMode.None;
}
