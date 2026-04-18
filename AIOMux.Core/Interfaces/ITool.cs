using AIOMux.Core.Models;
using AIOMux.Core.Policy;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract for tools that can be used by agents to perform specific tasks.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique name used for tool lookup.
    /// </summary>
    string Name { get; }

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
    /// <param name="input">The raw input string that would be passed to <see cref="ExecuteAsync"/>.</param>
    /// <returns>
    /// A <see cref="ToolExecutionAnalysis"/> describing the requested operations.
    /// </returns>
    ToolExecutionAnalysis Analyze(string input);

    /// <summary>
    /// Execute the tool with the given input and return a result string.
    /// </summary>
    /// <param name="input">The input string for the tool</param>
    /// <returns>The result of tool execution</returns>
    Task<string> ExecuteAsync(string input);
}