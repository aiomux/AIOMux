using AIOMux.Core.Models;

namespace AIOMux.Core.Replay;

/// <summary>
/// No-op implementation of IRuntimeEventSink that discards all events.
/// </summary>
public class NullRuntimeEventSink : IRuntimeEventSink
{
    public Task RecordAsync(RuntimeEvent evt, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}

