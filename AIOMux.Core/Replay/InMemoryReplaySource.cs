using AIOMux.Core.Models;

namespace AIOMux.Core.Replay;

/// <summary>
/// In-memory replay source that stores tool results by call ID.
/// </summary>
public class InMemoryReplaySource : IReplaySource
{
    private readonly Dictionary<string, ToolResult> _results = new(StringComparer.Ordinal);

    public void AddToolResult(string callId, ToolResult result)
    {
        _results[callId] = result;
    }

    public bool TryGetToolResult(string callId, out ToolResult result)
    {
        return _results.TryGetValue(callId, out result!);
    }
}
