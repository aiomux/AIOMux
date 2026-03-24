using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Extends IAgent to support cancellation tokens for long-running operations.
/// </summary>
public interface ICancellableAgent : IAgent
{
    /// <summary>
    /// Execute the agent with the given context and cancellation token.
    /// </summary>
    Task<string> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken);
}
