using AIOMux.Core.Interfaces;
using AIOMux.Core.Memory;
using AIOMux.Core.Models;
using AIOMux.Core.Replay;

namespace AIOMux.Core;

/// <summary>
/// Represents the context for agent execution, including user input and working directory.
/// </summary>
public class AgentContext
{
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
    /// Executes a tool by name, resolving from Tools.
    /// </summary>
    public async Task<ToolResult> ExecuteToolAsync(string toolName, string jsonArgs, CancellationToken ct = default)
    {
        if (!Tools.TryGetValue(toolName, out var tool))
        {
            return new ToolResult
            {
                CallId = Guid.NewGuid().ToString(),
                JsonResult = string.Empty,
                Success = false,
                Error = $"Tool not found: {toolName}"
            };
        }
        var call = new ToolCall
        {
            ToolName = toolName,
            JsonArgs = jsonArgs
        };
        var output = await tool.ExecuteAsync(jsonArgs);
        return new ToolResult
        {
            CallId = call.CallId,
            JsonResult = output,
            Success = true
        };
    }
}
