using AIOMux.Core.Models;
using AIOMux.Core.Policy;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Base class for all executable tools.
/// Tool execution is intentionally routed through an internal method so only the
/// core dispatcher can invoke tools.
/// </summary>
public abstract class DispatchableToolBase : ITool
{
    public abstract string Name { get; }

    public abstract IReadOnlyCollection<ToolOperation> SupportedOperations { get; }

    public abstract ToolExecutionAnalysis Analyze(string input);

    /// <summary>
    /// Internal execution entry point reserved for the dispatcher.
    /// </summary>
    internal Task<string> InvokeAsync(string input) => InvokeCoreAsync(input);

    /// <summary>
    /// Tool implementation hook for execution logic.
    /// </summary>
    protected abstract Task<string> InvokeCoreAsync(string input);
}
