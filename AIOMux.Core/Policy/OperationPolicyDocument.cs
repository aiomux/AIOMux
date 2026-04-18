using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIOMux.Core.Policy;

/// <summary>
/// Deserializable model for a standalone operation-based policy file.
///
/// Expected JSON shape:
/// <code>
/// {
///   "version": "1",
///   "tools": {
///     "file":  { "allowedOperations": ["Read", "Write"] },
///     "shell": { "allowedOperations": ["Read"] },
///     "http":  { "allowedOperations": ["Network"] }
///   }
/// }
/// </code>
///
/// Rules:
///   - A tool absent from <see cref="Tools"/> is denied by default.
///   - A tool present but with null or empty <see cref="ToolPolicyEntry.AllowedOperations"/> is denied by default.
///   - Unknown <see cref="ToolOperation"/> string values throw <see cref="JsonException"/> at load time.
/// </summary>
public sealed class OperationPolicyDocument
{
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        // Enum strings are matched case-insensitively; unknown values throw JsonException.
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    /// <summary>Schema version. Currently only "1" is supported.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    /// <summary>
    /// Per-tool permission entries. Tool names are matched case-insensitively at runtime.
    /// </summary>
    [JsonPropertyName("tools")]
    public Dictionary<string, ToolPolicyEntry> Tools { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Deserializes an <see cref="OperationPolicyDocument"/> from a JSON string.
    /// Throws <see cref="JsonException"/> when the JSON is invalid or contains an unknown operation name.
    /// </summary>
    public static OperationPolicyDocument Deserialize(string json)
    {
        var doc = JsonSerializer.Deserialize<OperationPolicyDocument>(json, DeserializeOptions)
            ?? throw new JsonException("Policy document deserialized to null.");

        if (!string.Equals(doc.Version, "1", StringComparison.Ordinal))
            throw new JsonException($"Unsupported policy document version '{doc.Version}'. Expected '1'.");

        return doc;
    }

    /// <summary>
    /// Reads and deserializes an <see cref="OperationPolicyDocument"/> from a file on disk.
    /// </summary>
    public static OperationPolicyDocument LoadFromFile(string path) =>
        Deserialize(File.ReadAllText(path));

    /// <summary>
    /// Converts the document into the permissions map consumed by <see cref="OperationPolicyEngine"/>.
    /// Tools with null or empty <c>allowedOperations</c> are included as empty sets,
    /// which the engine treats as deny-all for that tool.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyCollection<ToolOperation>> ToPermissions()
    {
        var result = new Dictionary<string, IReadOnlyCollection<ToolOperation>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (toolName, entry) in Tools)
            result[toolName] = entry.AllowedOperations ?? [];
        return result;
    }

    /// <summary>
    /// Produces a short SHA-256 hex digest of the serialized document for use as a policy hash.
    /// </summary>
    public string ComputeHash()
    {
        var canonical = JsonSerializer.Serialize(this, DeserializeOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}

/// <summary>
/// Allowed-operations entry for a single tool within an <see cref="OperationPolicyDocument"/>.
/// </summary>
public sealed class ToolPolicyEntry
{
    /// <summary>
    /// Explicit list of permitted operations for this tool.
    /// Null or empty means the tool is denied by default.
    /// </summary>
    [JsonPropertyName("allowedOperations")]
    public IReadOnlyCollection<ToolOperation>? AllowedOperations { get; init; }
}
