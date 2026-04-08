using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Core.Replay;

/// <summary>
/// Coordinates fork/replay state reconstruction and then executes through the runtime.
/// </summary>
public sealed class ForkReplayExecutor
{
    private readonly IExecutionRuntime _runtime;

    public ForkReplayExecutor(IExecutionRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public async Task<ExecutionResult> ExecuteForkAsync(
        string sourceRunId,
        int stepIndex,
        ExecutionPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourceRunId))
            return new ExecutionResult { Success = false, Error = "SourceRunId is required for fork execution" };
        if (plan == null)
            return new ExecutionResult { Success = false, Error = "Fork plan cannot be null" };
        if (context == null)
            return new ExecutionResult { Success = false, Error = "Fork context cannot be null" };

        try
        {
            var sourceRecords = await ForkReplayHelper.LoadRecordsAsync(sourceRunId, context.WorkingDirectory, cancellationToken);
            if (sourceRecords.Count == 0)
                return new ExecutionResult { Success = false, Error = $"No execution records found for run '{sourceRunId}'" };

            if (!ForkReplayHelper.TryRebuildStateUpToStep(sourceRunId, plan, context, sourceRecords, stepIndex, out var summary, out var hydrateError))
                return new ExecutionResult { Success = false, Error = hydrateError };

            context.State["fork.sourceRunId"] = sourceRunId;
            context.State["fork.stepIndex"] = stepIndex;
            context.State["fork.summary"] = summary;

            var services = context.Services ?? new ExecutionRuntimeServices();
            context.Services = services;
            services.ReplayToolResults = services.ReplayMode == ReplayMode.None
                ? new Dictionary<string, ToolResult>(StringComparer.Ordinal)
                : ForkReplayHelper.BuildReplayToolResults(sourceRecords, stepIndex);

            return await _runtime.ExecuteAsync(plan, context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { Success = false, Error = "Fork execution was cancelled" };
        }
        catch (Exception ex)
        {
            return new ExecutionResult { Success = false, Error = $"Unexpected error in fork execution: {ex.Message}" };
        }
    }
}
