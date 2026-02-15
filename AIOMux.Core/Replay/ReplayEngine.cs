using AIOMux.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIOMux.Core.Replay;

/// <summary>
/// Replays a recorded run from JSONL events without re-executing tools or calling LLMs.
/// </summary>
public class ReplayEngine
{
    private readonly string _runsDirectory;

    public ReplayEngine(string? basePath = null)
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aiomux",
            "runs"
        );

        _runsDirectory = basePath ?? defaultPath;
    }

    /// <summary>
    /// Loads and replays a run.
    /// </summary>
    public async Task<ReplayResult> ReplayAsync(string runId, bool validateHashes = true)
    {
        var filePath = Path.Combine(_runsDirectory, $"{runId}.jsonl");

        if (!File.Exists(filePath))
        {
            return new ReplayResult
            {
                Success = false,
                Error = $"Run file not found: {runId}"
            };
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            var events = new List<RuntimeEvent>();

            // Parse all events
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var evt = ParseEvent(line);
                if (evt != null)
                {
                    events.Add(evt);
                }
            }

            // Validate ordering
            if (!ValidateOrdering(events, out var orderError))
            {
                return new ReplayResult
                {
                    Success = false,
                    Error = $"Event ordering validation failed: {orderError}"
                };
            }

            // Validate hashes if requested
            if (validateHashes && !ValidateHashes(events, out var hashError))
            {
                return new ReplayResult
                {
                    Success = false,
                    Error = $"Hash validation failed: {hashError}"
                };
            }

            // Reconstruct timeline
            var result = ReconstructTimeline(events);
            result.RunId = runId;
            result.Events = events;

            return result;
        }
        catch (Exception ex)
        {
            return new ReplayResult
            {
                Success = false,
                Error = $"Replay failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Parses a JSONL line into a RuntimeEvent.
    /// </summary>
    private RuntimeEvent? ParseEvent(string json)
    {
        try
        {
            var baseEvent = JsonSerializer.Deserialize<RuntimeEvent>(json);
            if (baseEvent == null)
            {
                return null;
            }

            // Deserialize based on type
            return baseEvent.Type switch
            {
                "RunStarted" => JsonSerializer.Deserialize<RunStartedEvent>(json),
                "InputReceived" => JsonSerializer.Deserialize<InputReceivedEvent>(json),
                "StepCompleted" => JsonSerializer.Deserialize<StepCompletedEvent>(json),
                "ToolInvoked" => JsonSerializer.Deserialize<ToolInvokedEvent>(json),
                "ToolResult" => JsonSerializer.Deserialize<ToolResultEvent>(json),
                "RunFinished" => JsonSerializer.Deserialize<RunFinishedEvent>(json),
                _ => baseEvent
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validates that events are in correct sequence order.
    /// </summary>
    private bool ValidateOrdering(List<RuntimeEvent> events, out string? error)
    {
        error = null;

        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Seq != i)
            {
                error = $"Event at index {i} has sequence {events[i].Seq}, expected {i}";
                return false;
            }
        }

        // First event must be RunStarted
        if (events.Count > 0 && events[0].Type != "RunStarted")
        {
            error = "First event must be RunStarted";
            return false;
        }

        // Last event should be RunFinished (but not required for incomplete runs)
        return true;
    }

    /// <summary>
    /// Validates payload hashes.
    /// </summary>
    private bool ValidateHashes(List<RuntimeEvent> events, out string? error)
    {
        error = null;

        foreach (var evt in events)
        {
            if (evt.Payload != null && !string.IsNullOrEmpty(evt.PayloadHash))
            {
                var payloadJson = JsonSerializer.Serialize(evt.Payload);
                var computedHash = ComputeHash(payloadJson);

                if (!string.Equals(computedHash, evt.PayloadHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Hash mismatch for event {evt.Seq} (type: {evt.Type})";
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Reconstructs the timeline from events.
    /// </summary>
    private ReplayResult ReconstructTimeline(List<RuntimeEvent> events)
    {
        var result = new ReplayResult { Success = true };
        var steps = new List<ReplayStep>();
        var toolCalls = new List<ReplayToolCall>();

        string? currentInput = null;
        DateTime? startTime = null;
        DateTime? endTime = null;

        foreach (var evt in events)
        {
            if (evt.Type == "RunStarted")
            {
                startTime = evt.TimestampUtc;
                var payload = JsonSerializer.Deserialize<RunStartedEvent.RunStartedPayload>(
                    JsonSerializer.Serialize(evt.Payload)
                );
                if (payload != null)
                {
                    result.PipelineName = payload.PipelineName;
                }
            }
            else if (evt.Type == "InputReceived")
            {
                var payload = JsonSerializer.Deserialize<InputReceivedEvent.InputReceivedPayload>(
                    JsonSerializer.Serialize(evt.Payload)
                );
                if (payload != null)
                {
                    currentInput = payload.Input;
                    result.Input = currentInput;
                }
            }
            else if (evt.Type == "StepCompleted")
            {
                var payload = JsonSerializer.Deserialize<StepCompletedEvent.StepCompletedPayload>(
                    JsonSerializer.Serialize(evt.Payload)
                );
                if (payload != null)
                {
                    steps.Add(new ReplayStep
                    {
                        StepName = payload.StepName,
                        Output = payload.Output,
                        DurationMs = payload.DurationMs,
                        Timestamp = evt.TimestampUtc
                    });
                }
            }
            else if (evt.Type == "ToolInvoked")
            {
                var payload = JsonSerializer.Deserialize<ToolInvokedEvent.ToolInvokedPayload>(
                    JsonSerializer.Serialize(evt.Payload)
                );
                if (payload != null)
                {
                    toolCalls.Add(new ReplayToolCall
                    {
                        ToolName = payload.ToolName,
                        Args = payload.Args,
                        Timestamp = evt.TimestampUtc
                    });
                }
            }
            else if (evt.Type == "ToolResult")
            {
                var payload = JsonSerializer.Deserialize<ToolResultEvent.ToolResultPayload>(
                    JsonSerializer.Serialize(evt.Payload)
                );
                if (payload != null && toolCalls.Count > 0)
                {
                    var lastCall = toolCalls[^1];
                    lastCall.Result = payload.Result;
                    lastCall.DurationMs = payload.DurationMs;
                    lastCall.Success = payload.Success;
                    lastCall.Error = payload.Error;
                }
            }
            else if (evt.Type == "RunFinished")
            {
                endTime = evt.TimestampUtc;
                var payload = JsonSerializer.Deserialize<RunFinishedEvent.RunFinishedPayload>(
                    JsonSerializer.Serialize(evt.Payload)
                );
                if (payload != null)
                {
                    result.FinalOutput = payload.FinalOutput;
                    result.Success = payload.Success;
                    result.Error = payload.Error;
                    result.TotalDurationMs = payload.TotalDurationMs;
                }
            }
        }

        result.Steps = steps;
        result.ToolCalls = toolCalls;
        result.StartTime = startTime;
        result.EndTime = endTime;

        return result;
    }

    /// <summary>
    /// Computes SHA256 hash of a string.
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}

/// <summary>
/// Result of replaying a run.
/// </summary>
public class ReplayResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? RunId { get; set; }
    public string? PipelineName { get; set; }
    public string? Input { get; set; }
    public string? FinalOutput { get; set; }
    public double TotalDurationMs { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<RuntimeEvent> Events { get; set; } = new();
    public List<ReplayStep> Steps { get; set; } = new();
    public List<ReplayToolCall> ToolCalls { get; set; } = new();
}

/// <summary>
/// A step reconstructed from replay.
/// </summary>
public class ReplayStep
{
    public string StepName { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// A tool call reconstructed from replay.
/// </summary>
public class ReplayToolCall
{
    public string ToolName { get; set; } = string.Empty;
    public string Args { get; set; } = string.Empty;
    public string? Result { get; set; }
    public double DurationMs { get; set; }
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }
}
