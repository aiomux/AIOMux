using AIOMux.Core.Models;
using System.Collections.Immutable;

namespace AIOMux.Core;

/// <summary>
/// Resolves step inputs by combining static input values with dynamic bindings.
/// Supports explicit binding paths: "inputs.key" (from entry payload) and "state.key" (from execution state).
/// </summary>
internal static class StepInputResolver
{
    /// <summary>
    /// Resolves all step inputs by merging static values with bound values from context.
    /// </summary>
    /// <param name="step">The execution step</param>
    /// <param name="ctx">The execution context</param>
    /// <returns>Resolved input values for the step</returns>
    public static ImmutableDictionary<string, object?> ResolveInputs(ExecutionStep step, ExecutionContext ctx)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, object?>(StringComparer.OrdinalIgnoreCase);

        // 1. Start with static inputs
        foreach (var (key, value) in step.Inputs)
        {
            builder[key] = value;
        }

        // 2. Overlay with bindings resolved from context
        foreach (var (key, bindingPath) in step.Bindings)
        {
            var resolvedValue = ResolvePath(bindingPath, ctx);
            builder[key] = resolvedValue;
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Resolves a single binding path from the execution context.
    /// Supports "inputs.key" and "state.key" paths.
    /// </summary>
    /// <param name="bindingPath">The binding path (e.g., "inputs.someKey" or "state.someKey")</param>
    /// <param name="ctx">The execution context</param>
    /// <returns>The resolved value, or null if path not found</returns>
    private static object? ResolvePath(string bindingPath, ExecutionContext ctx)
    {
        if (string.IsNullOrWhiteSpace(bindingPath))
            return null;

        var parts = bindingPath.Split('.', 2);
        if (parts.Length != 2)
            return null;

        var source = parts[0];
        var key = parts[1];

        return source switch
        {
            "inputs" => TryGetValue(ctx.Inputs, key),
            "state" => TryGetValue(ctx.State, key),
            _ => null  // Unknown source, ignore
        };
    }

    /// <summary>
    /// Safely retrieves a value from a dictionary by key (case-insensitive for state).
    /// </summary>
    private static object? TryGetValue(Dictionary<string, object?> dict, string key)
    {
        return dict.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Gets a single input value by key, with fallback.
    /// </summary>
    /// <param name="inputs">Resolved input dictionary</param>
    /// <param name="key">Input key</param>
    /// <param name="fallback">Value to return if key not found</param>
    /// <returns>The input value or fallback</returns>
    public static string GetInputString(ImmutableDictionary<string, object?> inputs, string key, string fallback = "")
    {
        return inputs.TryGetValue(key, out var value)
            ? value?.ToString() ?? fallback
            : fallback;
    }
}
