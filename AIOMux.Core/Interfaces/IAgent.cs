using AIOMux.Core.Models;
using System.Collections.Immutable;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract every agent must implement.
/// Provides execution capability, identity, and optional metadata for discovery and introspection.
/// Agents execute steps and rely on runtime dispatcher and policy flow for tool usage.
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
    /// Structured metadata describing this agent.
    /// Defaults to a metadata object populated from <see cref="Name"/> and <see cref="Description"/>.
    /// </summary>
    AgentMetadata Metadata => new()
    {
        Name = Name,
        Description = Description
    };

    /// <summary>
    /// Creates and returns an executable agent instance.
    /// Defaults to returning the current instance.
    /// </summary>
    /// <param name="context">Factory context containing LLM resolver and configuration.</param>
    IAgent CreateAgent(AgentFactoryContext context) => this;

    /// <summary>
    /// Initializes the agent with any required setup.
    /// Defaults to a successful no-op.
    /// </summary>
    /// <param name="configuration">Optional configuration parameters.</param>
    Task<bool> InitializeAsync(Dictionary<string, object>? configuration = null) => Task.FromResult(true);

    /// <summary>
    /// Cleans up any resources used by the agent.
    /// Defaults to a completed no-op.
    /// </summary>
    Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Executes the agent for a step using explicit resolved inputs and returns a step-level result.
    /// </summary>
    Task<StepExecutionResult> ExecuteAsync(
        ImmutableDictionary<string, object?> inputs,
        ExecutionContext context,
        CancellationToken cancellationToken = default);
}

