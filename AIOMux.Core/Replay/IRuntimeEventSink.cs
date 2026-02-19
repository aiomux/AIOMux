using AIOMux.Core.Models;

namespace AIOMux.Core.Replay
{
    /// <summary>
    /// Abstraction for recording runtime events.
    /// </summary>
    public interface IRuntimeEventSink
    {
        Task RecordAsync(RuntimeEvent evt, CancellationToken ct);
    }
}

