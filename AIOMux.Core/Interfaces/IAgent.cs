using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract every agent must implement (SRP, DIP).
/// </summary>
public interface IAgent
{
    /// <summary> Unique name used for lookup. </summary>
    string Name { get; }

    /// <summary>
    /// Execute the agent with the given context and return a result string.
    /// </summary>
    /// <param name="context">The context containing input and execution options</param>
    /// <returns>The result of agent execution</returns>
    Task<string> ExecuteAsync(ExecutionContext context);
}

