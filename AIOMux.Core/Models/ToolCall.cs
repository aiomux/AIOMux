using System;

namespace AIOMux.Core.Models
{
    public class ToolCall
    {
        public string CallId { get; set; } = Guid.NewGuid().ToString();
        public string ToolName { get; set; } = string.Empty;
        public string JsonArgs { get; set; } = string.Empty;
    }
}
