using System.Text.Json.Serialization;

namespace AIOMux.Local;

/// <summary>
/// Runtime execution mode for the solution.
/// Controls whether development-only features such as AllowAll policy are permitted.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ExecutionMode
{
    /// <summary>
    /// Development mode. AllowAll policy is permitted with a warning.
    /// </summary>
    Development,

    /// <summary>
    /// Production mode. AllowAll policy is rejected at startup.
    /// </summary>
    Production
}

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
    /// Execution mode for this solution. Defaults to <see cref="ExecutionMode.Development"/>.
    /// Set to <see cref="ExecutionMode.Production"/> to reject unsafe configurations such as AllowAll policy.
    /// </summary>
    public ExecutionMode Mode { get; set; } = ExecutionMode.Development;

    /// <summary>
    /// Path to the policy configuration file. Required for solution execution.
    /// Validation fails if this field is null or empty, or if the referenced file does not exist.
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

    /// <summary>
    /// Agent package paths to scan for <c>IAgent</c> implementations.
    /// Paths are relative to the solution.json file location.
    /// </summary>
    [JsonPropertyName("agents")]
    public IReadOnlyList<string> Agents { get; set; } = [];

    /// <summary>
    /// Tool package paths to scan for <c>ITool</c> implementations.
    /// Paths are relative to the solution.json file location.
    /// </summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<string> Tools { get; set; } = [];

    /// <summary>
    /// Connector package DLL paths to scan for <c>IConnector</c> implementations.
    /// Paths are relative to the solution.json file location.
    /// Example: <c>../connectors/AIOMux.Connectors.Console.dll</c>.
    /// </summary>
    [JsonPropertyName("connectors")]
    public IReadOnlyList<string> Connectors { get; set; } = [];

    /// <summary>
    /// Default entry agent for connector-originated events.
    /// When set, overrides the agent inferred from the first agent step in the plan.
    /// </summary>
    public string? EntryAgent { get; set; }

    /// <summary>
    /// Connector runtime declarations for this solution.
    /// Each entry identifies a connector type to instantiate and start in serve mode.
    /// </summary>
    [JsonPropertyName("connectorConfigurations")]
    public List<ConnectorDeclaration> ConnectorConfigurations { get; set; } = [];

    /// <summary>
    /// Named LLM profiles available to agents in this solution.
    /// Each key is a profile identifier (for example: "default", "reasoning").
    /// Agents may declare a preferred profile via <see cref="AgentMetadata.PreferredLlmProfile"/>.
    /// The <c>"default"</c> key is used when no specific profile is requested.
    /// </summary>
    public Dictionary<string, LlmConfiguration> LlmProfiles { get; set; } = [];
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

/// <summary>
/// Declares a connector to instantiate and run for this solution.
/// </summary>
public sealed class ConnectorDeclaration
{
    /// <summary>
    /// Connector type identifier. Matched against <c>IConnector.Name</c> on discovered implementations.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Logical name for this connector instance within the solution.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional connector-specific configuration from the solution manifest.
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = new();
}

/// <summary>
/// Optional language model configuration for planner and agent LLM usage.
/// </summary>
public sealed class LlmConfiguration
{
    /// <summary>
    /// LLM provider identifier (for example: "ollama" or "openai").
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// Provider endpoint base URL.
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Model name to use for completions.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Maximum number of requests allowed per minute.
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Name of the environment variable that holds the API key.
    /// Preferred over <see cref="ApiKey"/> when both are set.
    /// The resolved value is never logged or stored in execution records.
    /// </summary>
    public string? ApiKeyEnvironmentVariable { get; set; }

    /// <summary>
    /// Inline API key. Used as a fallback when <see cref="ApiKeyEnvironmentVariable"/> is not set
    /// or the referenced environment variable is empty.
    /// Prefer <see cref="ApiKeyEnvironmentVariable"/> to avoid storing credentials in solution.json.
    /// The value is never logged or stored in execution records.
    /// </summary>
    public string? ApiKey { get; set; }
}
