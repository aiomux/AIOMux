namespace AIOMux.Core.Models
{
    /// <summary>
    /// Represents a request to invoke a tool.
    /// </summary>
    public class ToolCall
    {
        /// <summary>
        /// Unique identifier for the tool call.
        /// </summary>
        public string CallId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Name of the tool to invoke.
        /// </summary>
        public string ToolName { get; set; } = string.Empty;

        /// <summary>
        /// Serialized tool arguments.
        /// </summary>
        public string JsonArgs { get; set; } = string.Empty;
    }
}
