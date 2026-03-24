using AIOMux.Core.Models;

namespace AIOMux.Core;

/// <summary>
/// Abstraction for dispatching tool calls with policy enforcement, event emission, and replay support.
/// </summary>
public interface IToolDispatcher
{
    /// <summary>
    /// Invokes a tool call with policy evaluation, event recording, and optional replay.
    /// </summary>
    Task<ToolResult> InvokeAsync(
        ToolCall call,
        ExecutionContext context,
        CancellationToken ct = default);
}


