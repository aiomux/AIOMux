using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Replay;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AIOMux.Core.Replay;

/// <summary>
/// Wraps an <see cref="IExecutionRuntime"/> and records execution events to a JSONL run file.
/// </summary>
public class RecordingAgentRuntime : IExecutionRuntime, IRuntimeEventSink
{
    private readonly IExecutionRuntime _innerRuntime;
    private readonly RunRecorder _recorder;
    private readonly ILogger? _logger;
    private readonly bool _enableRecording;

    public RecordingAgentRuntime(
        IExecutionRuntime innerRuntime,
        RunRecorder recorder,
        bool enableRecording = true,
        ILogger? logger = null)
    {
        _innerRuntime = innerRuntime ?? throw new ArgumentNullException(nameof(innerRuntime));
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _enableRecording = enableRecording;
        _logger = logger;
    }

    public async Task RecordAsync(RuntimeEvent evt, CancellationToken ct)
        => await _recorder.RecordEventAsync(evt);

    public async Task<ExecutionResult> RunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default)
    {
        if (!_enableRecording)
            return await _innerRuntime.RunAsync(request, cancellationToken);

        var sw = Stopwatch.StartNew();
        string? runId = null;

        try
        {
            var ctx = request.Context ?? new ExecutionContext();

            ctx.ToolDispatcher = new ToolDispatcher(this);

            var run = new Run
            {
                RunId = ctx.RunId,
                PipelineName = request.Plan?.Name ?? "unknown",
                WorkingDirectory = ctx.WorkingDirectory,
                StartedUtc = DateTime.UtcNow
            };

            runId = await _recorder.StartRunAsync(run);
            ctx.RunId = runId;

            var initialInput = ctx.Inputs.TryGetValue("input", out var v) ? v?.ToString() ?? string.Empty : string.Empty;

            await _recorder.RecordEventAsync(new InputReceivedEvent
            {
                Payload = new InputReceivedEvent.InputReceivedPayload
                {
                    Input = initialInput,
                    InputHash = ComputeHash(initialInput)
                }
            });

            var result = await _innerRuntime.RunAsync(
                new ExecutionRunRequest { Plan = request.Plan, Context = ctx },
                cancellationToken);

            sw.Stop();
            await _recorder.FinishRunAsync(result.Success, result.Output, result.Error, sw.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (runId != null)
            {
                try { await _recorder.FinishRunAsync(false, null, ex.Message, sw.Elapsed.TotalMilliseconds); }
                catch { }
            }
            throw;
        }
    }

    public async Task<ExecutionResult> ForkAsync(ExecutionForkRequest request, CancellationToken cancellationToken = default)
    {
        if (!_enableRecording)
            return await _innerRuntime.ForkAsync(request, cancellationToken);

        var sw = Stopwatch.StartNew();
        string? runId = null;

        try
        {
            if (request.Context == null)
                return new ExecutionResult { Success = false, Error = "Fork request context cannot be null" };

            var ctx = request.Context;

            var replayResult = await ForkReplayHelper.LoadReplayAsync(request.SourceRunId, cancellationToken);
            if (!replayResult.Success)
                return new ExecutionResult { Success = false, Error = replayResult.Error ?? "Failed to load replay data." };

            if (!ForkReplayHelper.TryHydrateContext(ctx, replayResult.Events, request.EventIndex, out var hydrateError))
                return new ExecutionResult { Success = false, Error = hydrateError };

            var newRunId = Guid.NewGuid().ToString();
            ctx.RunId = newRunId;
            ctx.State["fork.sourceRunId"] = request.SourceRunId;
            ctx.ReplayMode = request.ReplayMode;
            ctx.ReplaySource = request.ReplayMode == ReplayMode.None
                ? null
                : ForkReplayHelper.BuildReplaySource(request.SourceRunId, newRunId);

            ctx.ToolDispatcher = new ToolDispatcher(this, request.PolicyEngine);

            var run = new Run
            {
                RunId = newRunId,
                PipelineName = request.Plan?.Name ?? "unknown",
                WorkingDirectory = ctx.WorkingDirectory,
                StartedUtc = DateTime.UtcNow
            };

            runId = await _recorder.StartRunAsync(run);

            var initialInput = ctx.Inputs.TryGetValue("input", out var v) ? v?.ToString() ?? string.Empty : string.Empty;

            await _recorder.RecordEventAsync(new InputReceivedEvent
            {
                Payload = new InputReceivedEvent.InputReceivedPayload
                {
                    Input = initialInput,
                    InputHash = ComputeHash(initialInput)
                }
            });

            var result = await _innerRuntime.RunAsync(
                new ExecutionRunRequest { Plan = request.Plan, Context = ctx },
                cancellationToken);

            sw.Stop();
            await _recorder.FinishRunAsync(result.Success, result.Output, result.Error, sw.Elapsed.TotalMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (runId != null)
            {
                try { await _recorder.FinishRunAsync(false, null, ex.Message, sw.Elapsed.TotalMilliseconds); }
                catch { }
            }
            throw;
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

