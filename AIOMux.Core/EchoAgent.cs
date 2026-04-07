using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using System.Collections.Immutable;

namespace AIOMux.Core;

/// <summary>
/// Minimal built-in agent that returns the current input unchanged.
/// </summary>
public sealed class EchoAgent : IAgent
{
    public string Name => "echo";

    public string Description => "Returns the input unchanged.";

    public Task<StepExecutionResult> ExecuteAsync(
        ImmutableDictionary<string, object?> inputs,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var input = StepInputResolver.GetInputString(inputs, "input", context.GetInput());

        return Task.FromResult(new StepExecutionResult
        {
            Success = true,
            Output = input
        });
    }
}
