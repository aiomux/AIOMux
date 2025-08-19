namespace AIOMux.Core.Interfaces;

/// <summary>
/// Defines capabilities and metadata that an agent exposes to the system.
/// </summary>
public interface IAgentCapabilities
{
    /// <summary>
    /// Description of what the agent does and how to use it.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Tags for categorizing and discovering the agent.
    /// </summary>
    IReadOnlyCollection<string> Tags { get; }

    /// <summary>
    /// Indicates whether the agent supports being part of a chain.
    /// </summary>
    bool SupportsChaining { get; }
}