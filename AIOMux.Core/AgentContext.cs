using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Core;

/// <summary>
/// Represents the context for agent execution, including user input and working directory.
/// </summary>
public class AgentContext
{
    /// <summary>
    /// Gets or sets the user input for the agent.
    /// </summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory for the agent.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets the variables available in the agent context.
    /// </summary>
    public Dictionary<string, object> Variables { get; } = new();

    /// <summary>
    /// Gets or sets the options controlling agent execution behavior.
    /// </summary>
    public ExecutionOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the dictionary of tools available to agents during execution.
    /// </summary>
    public Dictionary<string, ITool> Tools { get; set; } = new();

    /// <summary>
    /// Gets or sets the memory store for persisting data between agent executions.
    /// </summary>
    public IMemoryStore Memory { get; set; } = new Memory.InMemoryStore();

    /// <summary>
    /// Gets or sets the agent manager for accessing available agents.
    /// </summary>
    public IAgentManager? AgentManager { get; set; }
}
