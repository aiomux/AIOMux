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
    /// <param name="call">The tool call to invoke.</param>
    /// <param name="context">The agent context for execution.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the tool invocation.</returns>
    Task<ToolResult> InvokeAsync(ToolCall call, AgentContext context, CancellationToken ct = default);
}
