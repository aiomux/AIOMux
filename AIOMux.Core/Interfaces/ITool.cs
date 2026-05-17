using AIOMux.Core.Models;
using AIOMux.Core.Policy;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract for tools that can be used by agents to perform specific tasks.
/// Execution is intentionally not exposed here and is only available through the dispatcher path.
/// Tools do not self-authorize; policy evaluation is required before dispatch.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique name used for tool lookup.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Structured metadata describing this tool for planning, docs, and UI scenarios.
    /// Descriptor metadata does not authorize execution and does not bypass policy.
    /// </summary>
    ToolDescriptor Descriptor { get; }

    /// <summary>
    /// The complete set of operations this tool can ever perform across all invocations.
    /// Used by policy engines to pre-screen tool registration or plan loading.
    /// </summary>
    IReadOnlyCollection<ToolOperation> SupportedOperations { get; }

    /// <summary>
    /// Analyzes the given input and classifies which operations this specific invocation
    /// would perform. Must be deterministic and side-effect-free.
    /// When the input cannot be classified, return <see cref="ToolExecutionAnalysis.Unrecognized"/>.
    /// </summary>
    /// <param name="input">The raw input string intended for tool execution.</param>
    /// <returns>
    /// A <see cref="ToolExecutionAnalysis"/> describing the requested operations.
    /// </returns>
    ToolExecutionAnalysis Analyze(string input);
}
