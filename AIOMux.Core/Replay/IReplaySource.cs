using AIOMux.Core.Models;

namespace AIOMux.Core.Replay;

/// <summary>
/// Provides recorded tool results for replay.
/// </summary>
public interface IReplaySource
{
    /// <summary>
    /// Attempts to retrieve a recorded tool result by call ID.
    /// </summary>
    bool TryGetToolResult(string callId, out ToolResult result);
}
