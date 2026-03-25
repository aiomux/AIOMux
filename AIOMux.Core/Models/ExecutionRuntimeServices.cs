using AIOMux.Core.Interfaces;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core.Models;

/// <summary>
/// Runtime dependencies and execution behavior settings used by <see cref="ExecutionRuntime"/>.
/// Keeps service-like concerns separate from run-state data in <see cref="ExecutionContext"/>.
/// </summary>
public sealed class ExecutionRuntimeServices
{
    /// <summary>
    /// Tools available to tool steps.
    /// </summary>
    public Dictionary<string, ITool> Tools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Agent registry used by agent steps.
    /// </summary>
    public IAgentManager? AgentManager { get; set; }

    /// <summary>
    /// Policy engine used to evaluate step execution.
    /// </summary>
    public IPolicyEngine PolicyEngine { get; set; } = new AllowAllPolicyEngine();

    /// <summary>
    /// Execution behavior options.
    /// </summary>
    public ExecutionOptions Options { get; set; } = new();

    /// <summary>
    /// Replay mode for tool execution.
    /// </summary>
    public ReplayMode ReplayMode { get; set; } = ReplayMode.None;

    /// <summary>
    /// Replayed tool results keyed by deterministic replay key.
    /// </summary>
    public Dictionary<string, ToolResult> ReplayToolResults { get; set; } = new(StringComparer.Ordinal);
}
