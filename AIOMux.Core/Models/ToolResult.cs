namespace AIOMux.Core.Models
{
    public class ToolResult
    {
        public string CallId { get; set; } = string.Empty;
        public string JsonResult { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
