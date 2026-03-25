namespace AIOMux.Core.Models
{
    /// <summary>
    /// Represents the result of a tool invocation.
    /// </summary>
    public class ToolResult
    {
        /// <summary>
        /// Unique identifier for the tool call.
        /// </summary>
        public string CallId { get; set; } = string.Empty;

        /// <summary>
        /// Serialized result payload from the tool.
        /// </summary>
        public string JsonResult { get; set; } = string.Empty;

        /// <summary>
        /// Indicates whether the tool execution succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message when execution fails.
        /// </summary>
        public string? Error { get; set; }
    }
}
