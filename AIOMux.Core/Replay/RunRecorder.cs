using AIOMux.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIOMux.Core.Replay;

/// <summary>
/// Records runtime events to append-only JSONL files.
/// Thread-safe, atomic appends.
/// </summary>
public class RunRecorder : IDisposable
{
    private readonly string _runsDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private StreamWriter? _currentWriter;
    private string? _currentRunId;
    private int _currentSeq = 0;

    /// <summary>
    /// Creates a new run recorder.
    /// </summary>
    /// <param name="basePath">Base path for storing runs. Defaults to ~/.aiomux/runs</param>
    public RunRecorder(string? basePath = null)
    {
        var defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aiomux",
            "runs"
        );

        _runsDirectory = basePath ?? defaultPath;
        Directory.CreateDirectory(_runsDirectory);
    }

    /// <summary>
    /// Starts recording a new run.
    /// </summary>
    public async Task<string> StartRunAsync(Run run)
    {
        await _lock.WaitAsync();
        try
        {
            // Close previous run if any
            if (_currentWriter != null)
            {
                await _currentWriter.FlushAsync();
                _currentWriter.Dispose();
            }

            _currentRunId = run.RunId;
            _currentSeq = 0;

            var filePath = GetRunFilePath(run.RunId);

            // Create new writer with atomic append
            _currentWriter = new StreamWriter(
                new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read),
                Encoding.UTF8
            );

            // Emit RunStarted event
            var startEvent = new RunStartedEvent
            {
                RunId = run.RunId,
                Seq = _currentSeq++,
                Payload = new RunStartedEvent.RunStartedPayload
                {
                    PipelineName = run.PipelineName,
                    WorkingDirectory = run.WorkingDirectory
                }
            };

            await AppendEventAsync(startEvent);

            return run.RunId;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Records an event to the current run.
    /// </summary>
    public async Task RecordEventAsync(RuntimeEvent evt)
    {
        await _lock.WaitAsync();
        try
        {
            if (_currentRunId == null)
            {
                throw new InvalidOperationException("No run is currently active. Call StartRunAsync first.");
            }

            evt.RunId = _currentRunId;
            evt.Seq = _currentSeq++;

            await AppendEventAsync(evt);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Finishes the current run.
    /// </summary>
    public async Task FinishRunAsync(bool success, string? finalOutput = null, string? error = null, double totalDurationMs = 0)
    {
        await _lock.WaitAsync();
        try
        {
            if (_currentRunId == null)
            {
                return; // No active run
            }

            var finishEvent = new RunFinishedEvent
            {
                RunId = _currentRunId,
                Seq = _currentSeq++,
                Payload = new RunFinishedEvent.RunFinishedPayload
                {
                    Success = success,
                    FinalOutput = finalOutput,
                    Error = error,
                    TotalDurationMs = totalDurationMs
                }
            };

            await AppendEventAsync(finishEvent);

            // Close the writer
            if (_currentWriter != null)
            {
                await _currentWriter.FlushAsync();
                _currentWriter.Dispose();
                _currentWriter = null;
            }

            _currentRunId = null;
            _currentSeq = 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Appends an event to the JSONL file.
    /// </summary>
    private async Task AppendEventAsync(RuntimeEvent evt)
    {
        if (_currentWriter == null)
        {
            throw new InvalidOperationException("No active writer");
        }

        // Compute payload hash
        if (evt.Payload != null)
        {
            var payloadJson = JsonSerializer.Serialize(evt.Payload);
            evt.PayloadHash = ComputeHash(payloadJson);
        }

        // Serialize to JSON
        var json = JsonSerializer.Serialize(evt, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        // Append line
        await _currentWriter.WriteLineAsync(json);
        await _currentWriter.FlushAsync();
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

    /// <summary>
    /// Gets the file path for a run.
    /// </summary>
    private string GetRunFilePath(string runId)
    {
        return Path.Combine(_runsDirectory, $"{runId}.jsonl");
    }

    /// <summary>
    /// Lists all recorded runs.
    /// </summary>
    public async Task<List<RunMetadata>> ListRunsAsync()
    {
        var files = Directory.GetFiles(_runsDirectory, "*.jsonl");
        var runs = new List<RunMetadata>();

        foreach (var file in files)
        {
            try
            {
                var runId = Path.GetFileNameWithoutExtension(file);
                var metadata = await GetRunMetadataAsync(runId);
                if (metadata != null)
                {
                    runs.Add(metadata);
                }
            }
            catch
            {
                // Skip corrupted files
            }
        }

        return runs.OrderByDescending(r => r.StartedUtc).ToList();
    }

    /// <summary>
    /// Gets metadata for a run by reading the first and last events.
    /// </summary>
    public async Task<RunMetadata?> GetRunMetadataAsync(string runId)
    {
        var filePath = GetRunFilePath(runId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            if (lines.Length == 0)
            {
                return null;
            }

            // Parse first event (RunStarted)
            var firstEvent = JsonSerializer.Deserialize<RuntimeEvent>(lines[0]);
            if (firstEvent == null)
            {
                return null;
            }

            // Parse last event if exists
            RuntimeEvent? lastEvent = null;
            if (lines.Length > 1)
            {
                lastEvent = JsonSerializer.Deserialize<RuntimeEvent>(lines[^1]);
            }

            var metadata = new RunMetadata
            {
                RunId = runId,
                StartedUtc = firstEvent.TimestampUtc,
                EventCount = lines.Length
            };

            // Extract pipeline name from first event
            if (firstEvent is RunStartedEvent)
            {
                var payload = JsonSerializer.Deserialize<RunStartedEvent.RunStartedPayload>(
                    JsonSerializer.Serialize(firstEvent.Payload)
                );
                if (payload != null)
                {
                    metadata.PipelineName = payload.PipelineName;
                }
            }

            // Check if run finished
            if (lastEvent?.Type == "RunFinished")
            {
                metadata.CompletedUtc = lastEvent.TimestampUtc;
                var payload = JsonSerializer.Deserialize<RunFinishedEvent.RunFinishedPayload>(
                    JsonSerializer.Serialize(lastEvent.Payload)
                );
                if (payload != null)
                {
                    metadata.Success = payload.Success;
                }
            }

            return metadata;
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        _currentWriter?.Dispose();
        _lock.Dispose();
    }
}

/// <summary>
/// Metadata about a recorded run.
/// </summary>
public class RunMetadata
{
    public string RunId { get; set; } = string.Empty;
    public string? PipelineName { get; set; }
    public DateTime StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public bool? Success { get; set; }
    public int EventCount { get; set; }
}

