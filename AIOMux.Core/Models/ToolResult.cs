namespace AIOMux.Core.Models;

/// <summary>
/// Represents the structured result of a tool invocation.
/// </summary>
public sealed record ToolResult
{
    /// <summary>
    /// Unique identifier for the tool call.
    /// </summary>
    public string CallId { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the tool execution succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Serialized successful result payload from the tool.
    /// </summary>
    public string? JsonResult { get; init; }

    /// <summary>
    /// Stable error code for failures.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// Human-readable error message for failures.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
