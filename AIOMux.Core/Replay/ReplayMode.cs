namespace AIOMux.Core.Replay;

/// <summary>
/// Specifies the replay mode for tool execution.
/// </summary>
public enum ReplayMode
{
    /// <summary>
    /// Normal execution - tools are executed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Full replay - all tools return recorded results.
    /// </summary>
    Full = 1,

    /// <summary>
    /// Replay tools only - tools return recorded results, but other operations execute normally.
    /// </summary>
    ToolsOnly = 2
}
