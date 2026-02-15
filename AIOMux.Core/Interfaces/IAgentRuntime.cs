using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// High-level facade for running agents or chains from hosts (.Local, Web, Discord, etc.)
/// without exposing internal orchestration details.
/// </summary>
public interface IAgentRuntime
{
    /// <summary>
    /// Runs an agent or chain with the given request.
    /// </summary>
    /// <param name="request">The execution request specifying which agent or chain to run</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Structured result with success status, output, and optional error details</returns>
    Task<AgentRuntimeResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forks execution from a previous run and continues from a hydrated state.
    /// </summary>
    /// <param name="request">The fork request with source run info and new execution details</param>
    /// <param name="cancellationToken">Token to cancel the operation</param>
    /// <returns>Structured result with success status, output, and optional error details</returns>
    Task<AgentRuntimeResult> ForkAsync(AgentForkRequest request, CancellationToken cancellationToken = default);
}
