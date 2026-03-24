using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract every agent must implement.
/// Provides execution capability, identity, and optional metadata for discovery and introspection.
/// </summary>
public interface IAgent
{
    /// <summary>Unique name used for lookup and identification.</summary>
    string Name { get; }

    /// <summary>
    /// Human-readable description of what this agent does.
    /// Used for planning and introspection. Defaults to "No description available".
    /// </summary>
    string Description => "No description available";

    /// <summary>
    /// Tags for categorizing and discovering the agent.
    /// Defaults to an empty collection.
    /// </summary>
    IReadOnlyCollection<string> Tags => Array.Empty<string>();

    /// <summary>
    /// Indicates whether the agent can be used as a step within an <see cref="ExecutionPlan"/>.
    /// Defaults to true.
    /// </summary>
    bool SupportsPipelining => true;

    /// <summary>
    /// Executes the agent and returns a step-level result.
    /// </summary>
    Task<StepExecutionResult> ExecuteAsync(
        ExecutionContext context,
        CancellationToken cancellationToken = default);
}

