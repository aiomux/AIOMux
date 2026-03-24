using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// High-level facade for running execution plans without exposing internal orchestration details.
/// </summary>
public interface IExecutionRuntime
{
    /// <summary>
    /// Runs an execution plan with the provided context.
    /// </summary>
    Task<ExecutionResult> RunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forks execution from a previous run, hydrates context from recorded events,
    /// and continues with the provided plan.
    /// </summary>
    Task<ExecutionResult> ForkAsync(ExecutionForkRequest request, CancellationToken cancellationToken = default);
}
