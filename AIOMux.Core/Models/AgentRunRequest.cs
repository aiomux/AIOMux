namespace AIOMux.Core.Models;

/// <summary>
/// Request to run an agent or chain via the runtime facade.
/// Exactly one of AgentName or ChainName must be specified.
/// </summary>
public sealed record AgentRunRequest
{
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
}
