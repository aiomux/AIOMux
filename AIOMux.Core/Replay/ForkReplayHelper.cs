using AIOMux.Core.Models;

namespace AIOMux.Core.Replay;

internal static class ForkReplayHelper
{
    public static Task<List<ExecutionRecord>> LoadRecordsAsync(string runId, CancellationToken cancellationToken)
        => ExecutionRecordStore.LoadAsync(runId, cancellationToken);

    public static bool TryRebuildStateUpToStep(
        string sourceRunId,
        ExecutionPlan plan,
        ExecutionContext context,
        IReadOnlyList<ExecutionRecord> records,
        int stepIndex,
        out ReplayForkSummary? summary,
        out string? error)
    {
        error = null;
        summary = null;

        if (string.IsNullOrWhiteSpace(sourceRunId))
        {
            error = "Source run id is required for reconstruction.";
            return false;
        }

        if (records.Count == 0)
        {
            error = "No execution records are available for reconstruction.";
            return false;
        }

        if (plan.Steps.Count == 0)
        {
            error = "Execution plan has no steps to reconstruct.";
            return false;
        }

        if (stepIndex < 0 || stepIndex >= plan.Steps.Count)
        {
            error = $"Fork step index {stepIndex} is outside the plan step range 0..{plan.Steps.Count - 1}.";
            return false;
        }

        var selectedRecords = SelectRecordsUpToStep(records, stepIndex);
        if (selectedRecords.Count == 0)
        {
            error = $"No execution records found up to step index {stepIndex}.";
            return false;
        }

        foreach (var record in selectedRecords)
        {
            if (record.StepIndex < 0 || record.StepIndex >= plan.Steps.Count)
            {
                error = $"Execution record step index {record.StepIndex} is outside plan bounds.";
                return false;
            }

            var expectedStep = plan.Steps[record.StepIndex];
            if (!string.Equals(expectedStep.Id, record.StepId, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Execution record mismatch at index {record.StepIndex}: expected step '{expectedStep.Id}' but found '{record.StepId}'.";
                return false;
            }

            if (!context.Inputs.ContainsKey("input") && record.Input != null)
                context.Inputs["input"] = record.Input;

            context.State["stepIndex"] = record.StepIndex;

            if (!record.Success)
                continue;

            if (record.StateChanges.Count > 0)
            {
                foreach (var (key, value) in record.StateChanges)
                    context.State[key] = value;
            }
            else
            {
                var outputKey = string.IsNullOrWhiteSpace(record.OutputKey) ? record.Target : record.OutputKey;
                if (!string.IsNullOrWhiteSpace(outputKey))
                    context.State[outputKey] = record.Output;
            }
        }

        context.State["fork.reconstructedStepCount"] = selectedRecords.Count;
        context.State["fork.reconstructedStepIndex"] = selectedRecords[^1].StepIndex;

        summary = BuildSummary(sourceRunId, stepIndex, selectedRecords);
        return true;
    }

    public static Dictionary<string, ToolResult> BuildReplayToolResults(IReadOnlyList<ExecutionRecord> records, int upToStepIndex)
    {
        var replayResults = new Dictionary<string, ToolResult>(StringComparer.Ordinal);

        var selectedRecords = SelectRecordsUpToStep(records, upToStepIndex);

        foreach (var record in selectedRecords)
        {
            if (!record.Success || !record.Type.Equals("tool", StringComparison.OrdinalIgnoreCase))
                continue;

            var input = record.Input?.ToString() ?? string.Empty;
            var output = record.Output?.ToString() ?? string.Empty;
            var replayKey = DeterministicCallId.GenerateReplayKey(record.StepIndex.ToString(), record.Target, input);

            replayResults[replayKey] = new ToolResult
            {
                CallId = replayKey,
                Success = true,
                JsonResult = output,
                Error = null
            };
        }

        return replayResults;
    }

    private static List<ExecutionRecord> SelectRecordsUpToStep(IReadOnlyList<ExecutionRecord> records, int upToStepIndex)
        => records
            .Where(r => r.StepIndex <= upToStepIndex)
            .OrderBy(r => r.StepIndex)
            .ThenBy(r => r.Timestamp)
            .ToList();

    private static ReplayForkSummary BuildSummary(string sourceRunId, int stepIndex, IReadOnlyList<ExecutionRecord> selectedRecords)
    {
        var summary = new ReplayForkSummary
        {
            SourceRunId = sourceRunId,
            ForkStepIndex = stepIndex,
            ReconstructedRecordCount = selectedRecords.Count,
            LastReconstructedStepIndex = selectedRecords[^1].StepIndex,
            SuccessfulStepCount = selectedRecords.Count(r => r.Success),
            FailedStepCount = selectedRecords.Count(r => !r.Success),
            PolicyDeniedStepCount = selectedRecords.Count(r => !string.IsNullOrWhiteSpace(r.PolicyDenyReason)),
            ReplayedToolCallCount = selectedRecords.Count(r => r.Success && r.Type.Equals("tool", StringComparison.OrdinalIgnoreCase)),
            TotalDurationMs = selectedRecords.Sum(r => r.DurationMs)
        };

        foreach (var record in selectedRecords)
        {
            summary.Steps.Add(new ReplayStepSummary
            {
                StepId = record.StepId,
                StepIndex = record.StepIndex,
                Type = record.Type,
                Target = record.Target,
                Success = record.Success,
                DurationMs = record.DurationMs,
                Error = record.Error,
                PolicyDenyReason = record.PolicyDenyReason
            });

            if (!record.Type.Equals("tool", StringComparison.OrdinalIgnoreCase))
                continue;

            var input = record.Input?.ToString() ?? string.Empty;
            summary.ToolCalls.Add(new ReplayToolCallSummary
            {
                StepId = record.StepId,
                StepIndex = record.StepIndex,
                ToolName = record.Target,
                ReplayKey = DeterministicCallId.GenerateReplayKey(record.StepIndex.ToString(), record.Target, input),
                Success = record.Success,
                Input = input,
                Output = record.Output?.ToString(),
                Error = record.Error
            });
        }

        return summary;
    }
}

