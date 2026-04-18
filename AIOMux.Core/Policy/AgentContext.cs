namespace AIOMux.Core.Policy;

/// <summary>
/// Minimal execution context passed to <see cref="IPolicyEngine.Evaluate"/>.
/// Contains only the fields relevant to policy decisions.
/// </summary>
public sealed class AgentContext
{
    /// <summary>
    /// Unique identifier of the current execution run.
    /// </summary>
    public string RunId { get; init; } = string.Empty;
}
