using AIOMux.Core.Interfaces;
using AIOMux.Core.Memory;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;

namespace AIOMux.Core;

/// <summary>
/// Represents the context for agent execution, including user input and working directory.
/// </summary>
public class AgentContext
{
    private IToolDispatcher? _toolDispatcher;

    /// <summary>
    /// Gets or sets the user input for the agent.
    /// </summary>
    public string UserInput { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory for the agent.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Gets the variables available in the agent context.
    /// </summary>
    public Dictionary<string, object> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the options controlling agent execution behavior.
    /// </summary>
    public ExecutionOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the dictionary of tools available to agents during execution.
    /// </summary>
    public Dictionary<string, ITool> Tools { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the memory store for persisting data between agent executions.
    /// </summary>
    public IMemoryStore Memory { get; set; } = new NullMemoryStore();

    /// <summary>
    /// Gets or sets the agent manager for accessing available agents.
    /// </summary>
    public IAgentManager? AgentManager { get; set; }

    /// <summary>
    /// Gets or sets the replay mode for tool execution.
    /// </summary>
    public ReplayMode ReplayMode { get; set; } = ReplayMode.None;

    /// <summary>
    /// Gets or sets the replay source for retrieving recorded tool results.
    /// </summary>
    public IReplaySource? ReplaySource { get; set; }

    /// <summary>
    /// Gets the tool dispatcher for executing tools with policy enforcement and event recording.
    /// Lazily creates a default dispatcher if not explicitly set.
    /// </summary>
    public IToolDispatcher ToolDispatcher
    {
        get => _toolDispatcher ??= CreateDefaultDispatcher();
        set => _toolDispatcher = value;
    }

    private static IToolDispatcher CreateDefaultDispatcher()
    {
        var eventSink = new NullRuntimeEventSink();
        var policyEngine = new AllowAllPolicyEngine();
        return new ToolDispatcher(eventSink, policyEngine);
    }

    /// <summary>
    /// Executes a tool by name, resolving from Tools.
    /// </summary>
    public async Task<ToolResult> ExecuteToolAsync(string toolName, string jsonArgs, CancellationToken ct = default)
    {
        var callId = GenerateCallId(toolName, jsonArgs);

        if (!Tools.TryGetValue(toolName, out var tool))
        {
            return new ToolResult
            {
                CallId = callId,
                JsonResult = string.Empty,
                Success = false,
                Error = $"Tool not found: {toolName}"
            };
        }

        var call = new ToolCall
        {
            CallId = callId,
            ToolName = toolName,
            JsonArgs = jsonArgs
        };

        return await ToolDispatcher.InvokeAsync(call, this, ct);
    }

    private string GenerateCallId(string toolName, string jsonArgs)
    {
        var runId = Variables.TryGetValue("runId", out var runIdValue)
            ? runIdValue?.ToString() ?? string.Empty
            : string.Empty;
        var stepIndex = Variables.TryGetValue("stepIndex", out var stepIndexValue)
            ? stepIndexValue?.ToString() ?? string.Empty
            : string.Empty;
        return DeterministicCallId.Generate(runId, stepIndex, toolName, jsonArgs);
    }
}


