using System.Text.Json;

namespace AIOMux.Core.Models;

/// <summary>
/// Utilities for safe descriptor metadata projection and lightweight schema hints.
/// </summary>
public static class ToolDescriptorMetadataSerializer
{
    /// <summary>
    /// Creates a lightweight JSON schema hint for a CLR type.
    /// Returns null when type information is unavailable.
    /// </summary>
    public static string? TryCreateSchemaJson(Type? type)
    {
        if (type is null)
            return null;

        var schema = new Dictionary<string, object?>
        {
            ["title"] = GetSafeTypeName(type),
            ["type"] = MapJsonType(type)
        };

        return JsonSerializer.Serialize(schema);
    }

    /// <summary>
    /// Gets a safe type name for logs and debug output.
    /// Assembly-qualified names are excluded by default.
    /// </summary>
    public static string? GetSafeTypeName(Type? type, bool includeAssemblyQualifiedName = false)
    {
        if (type is null)
            return null;

        return includeAssemblyQualifiedName
            ? type.AssemblyQualifiedName
            : type.FullName ?? type.Name;
    }

    /// <summary>
    /// Converts a descriptor into a debug-safe projection suitable for JSON serialization.
    /// </summary>
    public static object ToDebugView(ToolDescriptor descriptor, bool includeAssemblyQualifiedTypeNames = false)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new
        {
            descriptor.Name,
            descriptor.Description,
            descriptor.Version,
            descriptor.Category,
            Operations = descriptor.Operations.Select(o => new
            {
                o.Name,
                o.Description,
                Operation = o.Operation.ToString(),
                RequestType = GetSafeTypeName(o.RequestType, includeAssemblyQualifiedTypeNames),
                ResponseType = GetSafeTypeName(o.ResponseType, includeAssemblyQualifiedTypeNames),
                o.RequestSchemaJson,
                o.ResponseSchemaJson,
                o.IsDangerous,
                o.IsReadOnly,
                o.Tags,
                o.ExampleJson,
                Constraints = o.Constraints.Select(c => new
                {
                    c.Name,
                    c.Description,
                    c.Type,
                    c.Required,
                    c.DefaultValue
                })
            })
        };
    }

    private static string MapJsonType(Type type)
    {
        var nonNullable = Nullable.GetUnderlyingType(type) ?? type;

        if (nonNullable == typeof(string) || nonNullable == typeof(Guid) || nonNullable == typeof(DateTime) || nonNullable == typeof(DateTimeOffset) || nonNullable == typeof(TimeSpan))
            return "string";

        if (nonNullable == typeof(bool))
            return "boolean";

        if (nonNullable.IsEnum)
            return "string";

        if (nonNullable == typeof(byte) || nonNullable == typeof(short) || nonNullable == typeof(int) || nonNullable == typeof(long) || nonNullable == typeof(sbyte) || nonNullable == typeof(ushort) || nonNullable == typeof(uint) || nonNullable == typeof(ulong))
            return "integer";

        if (nonNullable == typeof(float) || nonNullable == typeof(double) || nonNullable == typeof(decimal))
            return "number";

        if (nonNullable.IsArray || typeof(System.Collections.IEnumerable).IsAssignableFrom(nonNullable) && nonNullable != typeof(string))
            return "array";

        return "object";
    }
}
