namespace AIOMux.Core.Interfaces;

/// <summary>
/// Extends IAgent to support cancellation tokens for long-running operations.
/// </summary>
public interface ICancellableAgent : IAgent
{
    /// <summary>
    /// Execute the agent with the given context and cancellation token.
    /// </summary>
    /// <param name="context">The context containing input and execution options</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>The result of agent execution</returns>
    Task<string> ExecuteAsync(AgentContext context, CancellationToken cancellationToken);
}
