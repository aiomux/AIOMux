namespace AIOMux.Local;

/// <summary>
/// Defines a complete solution that can be executed locally.
/// Encompasses plan structure, policies, memory, and replay configuration.
/// </summary>
public sealed class SolutionDefinition
{
    /// <summary>
    /// Name of the solution.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the solution's purpose.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Entry point: path to the execution plan definition (e.g., "plans/main.json").
    /// Relative to the solution.json file location.
    /// </summary>
    public string Entry { get; set; } = string.Empty;

    /// <summary>
    /// Path to policy configuration file (optional).
    /// If null, defaults to AllowAllPolicyEngine.
    /// </summary>
    public string? PolicyConfig { get; set; }

    /// <summary>
    /// Path to memory store configuration (optional).
    /// Specifies how execution context memory is persisted.
    /// </summary>
    public string? MemoryConfig { get; set; }

    /// <summary>
    /// Replay configuration (optional).
    /// If specified, enables replaying previous runs.
    /// </summary>
    public ReplayConfiguration? Replay { get; set; }

    /// <summary>
    /// Execution options that affect runtime behavior.
    /// </summary>
    public ExecutionOptionsConfig? ExecutionOptions { get; set; }

    /// <summary>
    /// Working directory for the solution (optional).
    /// If not specified, uses the solution.json directory.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Additional metadata associated with the solution.
    /// </summary>
    public Dictionary<string, object?> Metadata { get; set; } = new();
}

/// <summary>
/// Replay configuration for a solution.
/// </summary>
public sealed class ReplayConfiguration
{
    /// <summary>
    /// Enable recording of execution events.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Directory where run records are stored.
    /// Relative to solution.json location.
    /// </summary>
    public string? StoragePath { get; set; }
}

/// <summary>
/// Execution options configuration for a solution.
/// </summary>
public sealed class ExecutionOptionsConfig
{
    /// <summary>
    /// Whether to collect execution metrics.
    /// </summary>
    public bool CollectMetrics { get; set; } = true;

    /// <summary>
    /// Whether to generate a summary report after execution.
    /// </summary>
    public bool GenerateJobSummary { get; set; } = true;

    /// <summary>
    /// Whether to include detailed metrics in the summary.
    /// </summary>
    public bool IncludeDetailedMetrics { get; set; }
}
