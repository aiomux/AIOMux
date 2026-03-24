using AIOMux.Core.Interfaces;
using AIOMux.Core.Memory;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core.Models;

/// <summary>
/// Holds all state and dependencies for a single execution run.
/// Replaces the former AgentContext as the canonical runtime context.
/// </summary>
public sealed class ExecutionContext
{
    private IToolDispatcher? _toolDispatcher;

    // ── Identity ────────────────────────────────────────────────────────────

    /// <summary>Unique identifier for this run.</summary>
    public string RunId { get; set; } = Guid.NewGuid().ToString();

    // ── Input / IO ──────────────────────────────────────────────────────────

    /// <summary>Primary user input for the current step.</summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>Working directory at execution time.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>Named inputs provided to the plan before execution begins.</summary>
    public Dictionary<string, object?> Inputs { get; set; } = new();

    // ── State ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Mutable state written by the runtime during execution.
    /// Stores step outputs (keyed by OutputKey) and tracking variables
    /// such as "stepIndex" and "user.input.original".
    /// </summary>
    public Dictionary<string, object?> State { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Trace ───────────────────────────────────────────────────────────────

    /// <summary>Execution records captured during this run.</summary>
    public List<ExecutionRecord> Records { get; set; } = new();

    // ── Options ─────────────────────────────────────────────────────────────

    /// <summary>Options controlling execution behavior.</summary>
    public ExecutionOptions Options { get; set; } = new();

    // ── Runtime dependencies ─────────────────────────────────────────────────

    /// <summary>Tools available to agents during execution.</summary>
    public Dictionary<string, ITool> Tools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Memory store for persisting data between steps.</summary>
    public IMemoryStore Memory { get; set; } = new NullMemoryStore();

    /// <summary>Agent registry used by agent steps and the planner.</summary>
    public IAgentManager? AgentManager { get; set; }

    // ── Replay ───────────────────────────────────────────────────────────────

    /// <summary>Replay mode for tool execution.</summary>
    public ReplayMode ReplayMode { get; set; } = ReplayMode.None;

    /// <summary>Replay source supplying recorded tool results.</summary>
    public IReplaySource? ReplaySource { get; set; }

    // ── Tool dispatch ────────────────────────────────────────────────────────

    /// <summary>
    /// Tool dispatcher with policy enforcement and event recording.
    /// Lazily initialised with safe defaults when not explicitly set.
    /// </summary>
    public IToolDispatcher ToolDispatcher
    {
        get => _toolDispatcher ??= CreateDefaultDispatcher();
        set => _toolDispatcher = value;
    }

    // ── Tool execution helper ────────────────────────────────────────────────

    /// <summary>Executes a tool by name, routing through the dispatcher.</summary>
    public async Task<ToolResult> ExecuteToolAsync(string toolName, string jsonArgs, CancellationToken ct = default)
    {
        var callId = GenerateCallId(toolName, jsonArgs);

        if (!Tools.TryGetValue(toolName, out _))
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

    private string GenerateCallId(string toolName, string jsonArgs)
    {
        var stepIndex = State.TryGetValue("stepIndex", out var stepIndexValue)
            ? stepIndexValue?.ToString() ?? string.Empty
            : string.Empty;
        return DeterministicCallId.Generate(RunId, stepIndex, toolName, jsonArgs);
    }

    private static IToolDispatcher CreateDefaultDispatcher()
        => new ToolDispatcher(new NullRuntimeEventSink(), new AllowAllPolicyEngine());
}
