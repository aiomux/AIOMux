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

    public virtual ToolDescriptor Descriptor => ToolDescriptorFactory.CreateFallback(Name, SupportedOperations);

    public abstract IReadOnlyCollection<ToolOperation> SupportedOperations { get; }

    public abstract ToolExecutionAnalysis Analyze(string input);

    /// <summary>
    /// Dispatcher-owned execution entry point.
    /// </summary>
    public Task<ToolResult> InvokeAsync(string input, CancellationToken cancellationToken = default)
        => InvokeCoreAsync(input, cancellationToken);

    /// <summary>
    /// Tool implementation hook for execution logic.
    /// </summary>
    protected abstract Task<ToolResult> InvokeCoreAsync(string input, CancellationToken cancellationToken = default);
}
