using AIOMux.Core.Interfaces;
using AIOMux.Core.Memory;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core.Models;

/// <summary>
/// First-class runtime context for a single execution run.
/// Carries identity, entry inputs, mutable state, execution trace, and all runtime dependencies.
/// </summary>
public sealed class ExecutionContext
{
    private IToolDispatcher? _toolDispatcher;

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Unique identifier for this run.</summary>
    public string RunId { get; set; } = Guid.NewGuid().ToString();

    // ── Entry payload ─────────────────────────────────────────────────────────

    /// <summary>
    /// Named inputs provided to the plan before execution begins.
    /// The primary user input is stored under the key <c>"input"</c>.
    /// </summary>
    public Dictionary<string, object?> Inputs { get; set; } = new();

    /// <summary>Working directory at execution time.</summary>
    public string? WorkingDirectory { get; set; }

    // ── Mutable execution state ───────────────────────────────────────────────

    /// <summary>
    /// Mutable key/value state written by the runtime during execution.
    /// <c>State["input"]</c> holds the resolved step input (updated by bindings).
    /// Step outputs are stored by <see cref="ExecutionStep.OutputKey"/>.
    /// </summary>
    public Dictionary<string, object?> State { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Execution trace ───────────────────────────────────────────────────────

    /// <summary>Canonical execution records captured during this run.</summary>
    public List<ExecutionRecord> Records { get; set; } = [];

    // ── Execution options ─────────────────────────────────────────────────────

    /// <summary>Options controlling execution behavior.</summary>
    public ExecutionOptions Options { get; set; } = new();

    // ── Runtime dependencies ──────────────────────────────────────────────────

    /// <summary>Tools available to steps during execution.</summary>
    public Dictionary<string, ITool> Tools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Memory store for persisting data between steps.</summary>
    public IMemoryStore Memory { get; set; } = new NullMemoryStore();

    /// <summary>Agent registry used by agent steps and the planner.</summary>
    public IAgentManager? AgentManager { get; set; }

    // ── Replay ────────────────────────────────────────────────────────────────

    /// <summary>Replay mode for tool execution.</summary>
    public ReplayMode ReplayMode { get; set; } = ReplayMode.None;

    /// <summary>Replay source supplying recorded tool results.</summary>
    public IReplaySource? ReplaySource { get; set; }

    // ── Tool dispatch ─────────────────────────────────────────────────────────

    /// <summary>
    /// Tool dispatcher with policy enforcement and event recording.
    /// Lazily initialised with safe defaults when not explicitly set.
    /// </summary>
    public IToolDispatcher ToolDispatcher
    {
        get => _toolDispatcher ??= new ToolDispatcher(new NullRuntimeEventSink(), new AllowAllPolicyEngine());
        set => _toolDispatcher = value;
    }

    // ── Input resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns the resolved step input: <c>State["input"]</c> when a binding has set it,
    /// falling back to the entry payload in <c>Inputs["input"]</c>.
    /// </summary>
    public string GetInput() =>
        (State.TryGetValue("input", out var s) ? s?.ToString() : null)
        ?? (Inputs.TryGetValue("input", out var i) ? i?.ToString() : null)
        ?? string.Empty;

    // ── Tool execution ────────────────────────────────────────────────────────

    /// <summary>Executes a tool by name, routing through the dispatcher.</summary>
    public async Task<ToolResult> ExecuteToolAsync(
        string toolName,
        string jsonArgs,
        CancellationToken ct = default)
    {
        var stepIndex = State.TryGetValue("stepIndex", out var stepIndexValue)
            ? stepIndexValue?.ToString() ?? string.Empty
            : string.Empty;

        var callId = DeterministicCallId.Generate(RunId, stepIndex, toolName, jsonArgs);

        if (!Tools.ContainsKey(toolName))
        {
            return new ToolResult
            {
                CallId = callId,
                JsonResult = string.Empty,
                Success = false,
                Error = $"Tool not found: {toolName}"
            };
        }

        var call = new ToolCall { CallId = callId, ToolName = toolName, JsonArgs = jsonArgs };
        return await ToolDispatcher.InvokeAsync(call, this, ct);
    }
}
