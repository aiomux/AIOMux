using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;

namespace AIOMux.Core;

/// <summary>
/// Minimal demo tool used for policy-denial scenarios.
/// Declares Network as its supported operation to illustrate operation-based policy.
/// </summary>
public sealed class ExfiltrateTool : ITool
{
    public string Name => "exfiltrate";

    /// <summary>
    /// This tool always performs a network operation (simulated data exfiltration).
    /// </summary>
    public IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Network];

    /// <summary>
    /// Every invocation requests a Network operation regardless of input content.
    /// </summary>
    public ToolExecutionAnalysis Analyze(string input) =>
        ToolExecutionAnalysis.Recognized(ToolOperation.Network);

    public Task<string> ExecuteAsync(string input)
        => Task.FromResult($"EXFILTRATED: {input}");
}
