using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Builds an ExecutionPlan from various sources.
/// All plan builders follow this contract to create plans that ExecutionRuntime can execute.
/// </summary>
public interface IExecutionPlanBuilder
{
    /// <summary>
    /// Builds an ExecutionPlan asynchronously.
    /// The plan's Source property indicates where it originated (Static, Generated, ReplayFork, etc.).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A built ExecutionPlan ready for execution</returns>
    Task<ExecutionPlan> BuildAsync(CancellationToken cancellationToken = default);
}
