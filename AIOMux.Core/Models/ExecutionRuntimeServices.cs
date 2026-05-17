using AIOMux.Core.Interfaces;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core.Models;

/// <summary>
/// Runtime dependencies and execution behavior settings used by <see cref="ExecutionRuntime"/>.
/// Keeps service-like concerns separate from run-state data in <see cref="ExecutionContext"/>.
/// A configured <see cref="PolicyEngine"/> is mandatory; runtime startup will fail if it is absent.
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
    /// Named LLM profile resolver used by runtime components and agents.
    /// </summary>
    public ILLMClientResolver? LlmClientResolver { get; set; }

    /// <summary>
    /// Policy engine used to evaluate step execution.
    /// A policy engine must be explicitly configured; there is no implicit AllowAll fallback.
    /// Runtime startup will throw if this is null.
    /// </summary>
    public IPolicyEngine? PolicyEngine { get; set; }

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
