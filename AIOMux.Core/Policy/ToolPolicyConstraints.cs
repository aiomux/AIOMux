using System.Text.Json.Serialization;

namespace AIOMux.Core.Policy;

/// <summary>
/// Per-operation target constraints for a tool policy entry.
/// Null or empty collections mean no explicit filter for that dimension.
/// </summary>
public sealed class ToolPolicyConstraints
{
    [JsonPropertyName("allowedPaths")]
    public IReadOnlyCollection<string>? AllowedPaths { get; init; }

    [JsonPropertyName("deniedPaths")]
    public IReadOnlyCollection<string>? DeniedPaths { get; init; }

    [JsonPropertyName("allowedHosts")]
    public IReadOnlyCollection<string>? AllowedHosts { get; init; }

    [JsonPropertyName("deniedHosts")]
    public IReadOnlyCollection<string>? DeniedHosts { get; init; }

    [JsonPropertyName("allowedSchemes")]
    public IReadOnlyCollection<string>? AllowedSchemes { get; init; }

    [JsonPropertyName("deniedSchemes")]
    public IReadOnlyCollection<string>? DeniedSchemes { get; init; }

    [JsonPropertyName("allowedPorts")]
    public IReadOnlyCollection<int>? AllowedPorts { get; init; }

    [JsonPropertyName("deniedPorts")]
    public IReadOnlyCollection<int>? DeniedPorts { get; init; }

    [JsonPropertyName("allowedCommands")]
    public IReadOnlyCollection<string>? AllowedCommands { get; init; }

    [JsonPropertyName("deniedCommands")]
    public IReadOnlyCollection<string>? DeniedCommands { get; init; }

    [JsonPropertyName("deniedArguments")]
    public IReadOnlyCollection<string>? DeniedArguments { get; init; }

    [JsonPropertyName("allowedServices")]
    public IReadOnlyCollection<string>? AllowedServices { get; init; }

    [JsonPropertyName("deniedServices")]
    public IReadOnlyCollection<string>? DeniedServices { get; init; }

    /// <summary>
    /// Returns true when any constraint list is populated.
    /// </summary>
    public bool HasAnyConstraint =>
        HasValues(AllowedPaths) ||
        HasValues(DeniedPaths) ||
        HasValues(AllowedHosts) ||
        HasValues(DeniedHosts) ||
        HasValues(AllowedSchemes) ||
        HasValues(DeniedSchemes) ||
        HasPorts(AllowedPorts) ||
        HasPorts(DeniedPorts) ||
        HasValues(AllowedCommands) ||
        HasValues(DeniedCommands) ||
        HasValues(DeniedArguments) ||
        HasValues(AllowedServices) ||
        HasValues(DeniedServices);

    private static bool HasValues(IReadOnlyCollection<string>? values) => values != null && values.Count > 0;
    private static bool HasPorts(IReadOnlyCollection<int>? ports) => ports != null && ports.Count > 0;
}
