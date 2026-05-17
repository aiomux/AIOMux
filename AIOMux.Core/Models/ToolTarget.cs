namespace AIOMux.Core.Models;

/// <summary>
/// Classifies an intended execution target reported by a tool analysis.
/// </summary>
public enum ToolTargetKind
{
    None = 0,
    FilePath,
    Url,
    Command,
    Process,
    Service,
    RegistryKey,
    EnvironmentVariable
}

/// <summary>
/// Represents a single execution target declared by a tool analysis.
/// </summary>
/// <param name="Kind">Target kind.</param>
/// <param name="Value">Raw target value.</param>
public sealed record ToolTarget(
    ToolTargetKind Kind,
    string Value);
