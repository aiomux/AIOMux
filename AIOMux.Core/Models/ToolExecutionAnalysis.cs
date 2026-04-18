using AIOMux.Core.Policy;

namespace AIOMux.Core.Models;

/// <summary>
/// The result of analyzing a single tool invocation before it executes.
/// Produced by <c>ITool.Analyze</c> to describe what the invocation intends to do.
/// </summary>
public sealed class ToolExecutionAnalysis
{
    /// <summary>
    /// The set of operations this specific invocation is requesting.
    /// Should be a subset of the tool's declared <c>SupportedOperations</c>.
    /// </summary>
    public IReadOnlyCollection<ToolOperation> RequestedOperations { get; init; } = [];

    /// <summary>
    /// Indicates whether the tool was able to classify the invocation.
    /// When false the runtime will deny execution regardless of policy.
    /// </summary>
    public bool IsRecognized { get; init; }

    /// <summary>
    /// Human-readable explanation, required when <see cref="IsRecognized"/> is false.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Creates a recognized analysis result with the given requested operations.
    /// </summary>
    public static ToolExecutionAnalysis Recognized(params ToolOperation[] operations) =>
        new() { IsRecognized = true, RequestedOperations = operations };

    /// <summary>
    /// Creates an unrecognized analysis result with a mandatory reason.
    /// </summary>
    public static ToolExecutionAnalysis Unrecognized(string reason) =>
        new() { IsRecognized = false, Reason = reason };
}
