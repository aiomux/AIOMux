using AIOMux.Core.Interfaces;

namespace AIOMux.Core;

/// <summary>
/// Context provided to agents during factory creation.
/// Provides access to named LLM profile resolution and optional configuration.
/// </summary>
public sealed class AgentFactoryContext
{
    /// <summary>
    /// Named LLM client resolver allowing agents to access multiple configured LLM profiles.
    /// Null when no resolver is available.
    /// </summary>
    public ILLMClientResolver? LlmResolver { get; init; }

    /// <summary>
    /// Optional configuration parameters for the agent.
    /// </summary>
    public Dictionary<string, object> Configuration { get; init; } = [];
}
