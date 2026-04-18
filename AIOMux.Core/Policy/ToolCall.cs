namespace AIOMux.Core.Policy;

/// <summary>
/// Represents a single tool invocation submitted for policy evaluation.
/// Passed to <see cref="IPolicyEngine.Evaluate"/> before the tool executes.
/// </summary>
public sealed class ToolCall
{
    /// <summary>
    /// Name of the tool being invoked, as registered in the tool registry.
    /// </summary>
    public string ToolName { get; init; } = string.Empty;

    /// <summary>
    /// Raw input string that will be forwarded to <c>ITool.ExecuteAsync</c>.
    /// </summary>
    public string Input { get; init; } = string.Empty;
}
