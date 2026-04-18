namespace AIOMux.Core.Policy;

/// <summary>
/// Classifies the category of operation a tool invocation may perform.
/// Tools declare which of these they can ever perform via <c>SupportedOperations</c>,
/// and report which ones a specific invocation requires via <c>ToolExecutionAnalysis.RequestedOperations</c>.
/// </summary>
public enum ToolOperation
{
    /// <summary>Reads data from a local source such as a file or registry key.</summary>
    Read,

    /// <summary>Writes or modifies data in a local destination such as a file or registry key.</summary>
    Write,

    /// <summary>Permanently removes data from a local destination.</summary>
    Delete,

    /// <summary>Opens outbound or inbound network connections.</summary>
    Network,

    /// <summary>Spawns or controls external OS processes.</summary>
    Process,

    /// <summary>Reads credentials, tokens, API keys, or other secrets.</summary>
    SecretRead,

    /// <summary>Reads rows or records from a database.</summary>
    DbRead,

    /// <summary>Inserts, updates, or deletes rows or records in a database.</summary>
    DbWrite
}
