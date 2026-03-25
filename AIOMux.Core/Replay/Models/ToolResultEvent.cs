using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

/// <summary>
/// Runtime event emitted with the final tool result.
/// </summary>
public class ToolResultEvent : RuntimeEvent
{
    public ToolResultEvent()
    {
        Type = "ToolResult";
    }

    /// <summary>
    /// Result produced by the tool call.
    /// </summary>
    public ToolResult? Result { get; set; }
}

