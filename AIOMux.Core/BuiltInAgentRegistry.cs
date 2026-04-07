using AIOMux.Core.Interfaces;

namespace AIOMux.Core;

/// <summary>
/// Resolves built-in agent implementations by logical agent name.
/// This is the narrow seam where externally loaded agent descriptors can be merged later.
/// </summary>
public static class BuiltInAgentRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> BuiltInAgentTypes =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["echo"] = typeof(EchoAgent),
            ["PlannerAgent"] = typeof(PlannerAgent)
        };

    public static Type ResolveType(
        string agentName,
        IReadOnlyDictionary<string, Type>? additionalAgentTypes = null)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new InvalidOperationException("Agent name is required.");

        if (additionalAgentTypes != null && additionalAgentTypes.TryGetValue(agentName, out var additionalType))
            return ValidateType(agentName, additionalType);

        if (BuiltInAgentTypes.TryGetValue(agentName, out var builtInType))
            return ValidateType(agentName, builtInType);

        throw new InvalidOperationException($"Unknown agent '{agentName}'.");
    }

    private static Type ValidateType(string agentName, Type implementationType)
    {
        if (!typeof(IAgent).IsAssignableFrom(implementationType) || implementationType.IsAbstract)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' is mapped to invalid implementation '{implementationType.FullName}'.");
        }

        if (implementationType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException(
                $"Agent '{agentName}' requires a parameterless constructor on '{implementationType.FullName}'.");
        }

        return implementationType;
    }
}
