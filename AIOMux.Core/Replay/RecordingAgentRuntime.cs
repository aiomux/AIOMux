using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AIOMux.Core.Replay;

/// <summary>
/// Wrapper around AgentRuntime that records execution events.
/// </summary>
public class RecordingAgentRuntime : IAgentRuntime, IRuntimeEventSink
{
    private readonly IAgentRuntime _innerRuntime;
    private readonly RunRecorder _recorder;
    private readonly ILogger? _logger;
    private readonly bool _enableRecording;

    public RecordingAgentRuntime(
        IAgentRuntime innerRuntime,
        RunRecorder recorder,
        bool enableRecording = true,
        ILogger? logger = null)
    {
        _innerRuntime = innerRuntime ?? throw new ArgumentNullException(nameof(innerRuntime));
        _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        _enableRecording = enableRecording;
        _logger = logger;
    }

    // IRuntimeEventSink implementation
    public async Task RecordAsync(RuntimeEvent evt, CancellationToken ct)
    {
        await _recorder.RecordEventAsync(evt);
    }

    public async Task<AgentRuntimeResult> RunAsync(
        AgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_enableRecording)
        {
            return await _innerRuntime.RunAsync(request, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        string? runId = null;

        try
        {
            // Set up ToolDispatcher with recording event sink
            if (request.Context != null)
            {
                request.Context.ToolDispatcher = new ToolDispatcher(this);
            }

            // Create run metadata
            var run = new Run
            {
                PipelineName = request.AgentName ?? request.ChainName ?? "unknown",
                WorkingDirectory = request.Context?.WorkingDirectory,
                StartedUtc = DateTime.UtcNow
            };

            // Start recording
            runId = await _recorder.StartRunAsync(run);

            if (request.Context != null)
            {
                request.Context.Variables["runId"] = runId;
            }

            // Record input
            var inputEvent = new InputReceivedEvent
            {
                Payload = new InputReceivedEvent.InputReceivedPayload
                {
                    Input = request.Context?.UserInput ?? string.Empty,
                    InputHash = ComputeHash(request.Context?.UserInput ?? string.Empty)
                }
            };
            await _recorder.RecordEventAsync(inputEvent);

            // Execute
            var result = await _innerRuntime.RunAsync(request, cancellationToken);

            stopwatch.Stop();

            // Record completion
            await _recorder.FinishRunAsync(
                success: result.Success,
                finalOutput: result.Output,
                error: result.Error,
                totalDurationMs: stopwatch.Elapsed.TotalMilliseconds
            );

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record failure
            if (runId != null)
            {
                try
                {
                    await _recorder.FinishRunAsync(
                        success: false,
                        finalOutput: null,
                        error: ex.Message,
                        totalDurationMs: stopwatch.Elapsed.TotalMilliseconds
                    );
                }
                catch
                {
                    // Ignore recording errors during exception handling
                }
            }

            throw;
        }
    }

    public async Task<AgentRuntimeResult> ForkAsync(
        AgentForkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_enableRecording)
        {
            return await _innerRuntime.ForkAsync(request, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        string? runId = null;

        try
        {
            if (request.Context == null)
            {
                return new AgentRuntimeResult { Success = false, Error = "Fork request context cannot be null" };
            }

            var replayResult = await ForkReplayHelper.LoadReplayAsync(request.SourceRunId, cancellationToken);
            if (!replayResult.Success)
            {
                return new AgentRuntimeResult
                {
                    Success = false,
                    Error = replayResult.Error ?? "Failed to load replay data."
                };
            }

            if (!ForkReplayHelper.TryHydrateContext(request.Context, replayResult.Events, request.EventIndex, out var hydrateError))
            {
                return new AgentRuntimeResult { Success = false, Error = hydrateError };
            }

            var newRunId = Guid.NewGuid().ToString();
            request.Context.Variables["runId"] = newRunId;
            request.Context.ReplayMode = request.ReplayMode;
            request.Context.ReplaySource = request.ReplayMode == ReplayMode.None
                ? null
                : ForkReplayHelper.BuildReplaySource(request.SourceRunId, newRunId);

            request.Context.ToolDispatcher = new ToolDispatcher(this, request.PolicyEngine);

            var run = new Run
            {
                RunId = newRunId,
                PipelineName = request.AgentName ?? request.ChainName ?? "unknown",
                WorkingDirectory = request.Context?.WorkingDirectory,
                StartedUtc = DateTime.UtcNow
            };

            runId = await _recorder.StartRunAsync(run);

            var inputEvent = new InputReceivedEvent
            {
                Payload = new InputReceivedEvent.InputReceivedPayload
                {
                    Input = request.Context?.UserInput ?? string.Empty,
                    InputHash = ComputeHash(request.Context?.UserInput ?? string.Empty)
                }
            };
            await _recorder.RecordEventAsync(inputEvent);

            var runRequest = new AgentRunRequest
            {
                AgentName = request.AgentName,
                ChainName = request.ChainName,
                Context = request.Context
            };

            var result = await _innerRuntime.RunAsync(runRequest, cancellationToken);

            stopwatch.Stop();

            await _recorder.FinishRunAsync(
                success: result.Success,
                finalOutput: result.Output,
                error: result.Error,
                totalDurationMs: stopwatch.Elapsed.TotalMilliseconds
            );

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            if (runId != null)
            {
                try
                {
                    await _recorder.FinishRunAsync(
                        success: false,
                        finalOutput: null,
                        error: ex.Message,
                        totalDurationMs: stopwatch.Elapsed.TotalMilliseconds
                    );
                }
                catch
                {
                }
            }

            throw;
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
