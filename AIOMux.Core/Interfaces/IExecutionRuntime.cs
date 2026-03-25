using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

public interface IExecutionRuntime
{
    Task<ExecutionResult> ExecuteAsync(
        ExecutionPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default);
}
