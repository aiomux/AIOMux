using AIOMux.Connectors.Console;
using AIOMux.Core.Interfaces;

namespace AIOMux.Connectors;

/// <summary>
/// Resolves built-in connector implementations by connector type.
/// This is the narrow seam where externally loaded connector descriptors can be merged later.
/// </summary>
public static class BuiltInConnectorRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> BuiltInConnectorTypes =
        new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
        {
            ["console"] = typeof(ConsoleConnector)
        };

    public static Type ResolveType(
        string connectorType,
        IReadOnlyDictionary<string, Type>? additionalConnectorTypes = null)
    {
        if (string.IsNullOrWhiteSpace(connectorType))
            throw new InvalidOperationException("Connector type is required.");

        if (additionalConnectorTypes != null && additionalConnectorTypes.TryGetValue(connectorType, out var additionalType))
            return ValidateType(connectorType, additionalType);

        if (BuiltInConnectorTypes.TryGetValue(connectorType, out var builtInType))
            return ValidateType(connectorType, builtInType);

        throw new InvalidOperationException($"Unknown connector type '{connectorType}'.");
    }

    public static IConnector Create(
        string connectorType,
        IReadOnlyDictionary<string, Type>? additionalConnectorTypes = null)
    {
        var implementationType = ResolveType(connectorType, additionalConnectorTypes);

        if (Activator.CreateInstance(implementationType) is not IConnector connector)
            throw new InvalidOperationException(
                $"Could not create connector '{connectorType}' from type '{implementationType.FullName}'.");

        return connector;
    }

    private static Type ValidateType(string connectorType, Type implementationType)
    {
        if (!typeof(IConnector).IsAssignableFrom(implementationType) || implementationType.IsAbstract)
        {
            throw new InvalidOperationException(
                $"Connector type '{connectorType}' is mapped to invalid implementation '{implementationType.FullName}'.");
        }

        if (implementationType.GetConstructor(Type.EmptyTypes) == null)
        {
            throw new InvalidOperationException(
                $"Connector type '{connectorType}' requires a parameterless constructor on '{implementationType.FullName}'.");
        }

        return implementationType;
    }
}
