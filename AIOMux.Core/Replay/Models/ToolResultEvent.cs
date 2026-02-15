using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

public class ToolResultEvent : RuntimeEvent
{
    public ToolResultEvent()
    {
        Type = "ToolResult";
    }

    public ToolResult? Result { get; set; }
}
