namespace AIOMux.Core.Interfaces;

/// <summary>
/// Extended agent interface that provides additional metadata and capabilities.
/// </summary>
public interface IAgentExtended : IAgent
{
    /// <summary>
    /// Gets detailed metadata about the agent.
    /// </summary>
    AgentMetadata Metadata { get; }

    /// <summary>
    /// Gets the agent's capabilities.
    /// </summary>
    IAgentCapabilities Capabilities { get; }
}