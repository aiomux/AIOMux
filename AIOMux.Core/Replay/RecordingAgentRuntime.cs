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
            // Create run metadata
            var run = new Run
            {
                PipelineName = request.AgentName ?? request.ChainName ?? "unknown",
                WorkingDirectory = request.Context?.WorkingDirectory,
                StartedUtc = DateTime.UtcNow
            };

            // Start recording
            runId = await _recorder.StartRunAsync(run);

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

    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
