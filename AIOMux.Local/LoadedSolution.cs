using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Local;

/// <summary>
/// Represents a fully loaded solution: a resolved execution plan, configured runtime services,
/// and any connectors discovered from the solution's declared connector packages.
/// </summary>
public sealed class LoadedSolution
{
    /// <summary>
    /// Name of the solution.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Description of the solution's purpose.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Working directory resolved for this solution.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Default entry agent for connector-originated events.
    /// Propagated from the manifest <c>entryAgent</c> field when set.
    /// </summary>
    public string? EntryAgent { get; init; }

    /// <summary>
    /// The resolved execution plan.
    /// </summary>
    public ExecutionPlan Plan { get; init; } = new();

    /// <summary>
    /// Runtime services (agents, tools, policy) configured for this solution.
    /// </summary>
    public ExecutionRuntimeServices Services { get; init; } = new();

    /// <summary>
    /// Resolved connector instances together with their manifest declarations.
    /// </summary>
    public IReadOnlyList<ResolvedConnector> Connectors { get; init; } = [];
}

/// <summary>
/// Represents a connector resolved for a solution together with its manifest declaration.
/// </summary>
public sealed class ResolvedConnector
{
    /// <summary>
    /// The connector instance.
    /// </summary>
    public IConnector Connector { get; init; } = default!;

    /// <summary>
    /// The manifest declaration that resolved this connector.
    /// </summary>
    public ConnectorDeclaration Declaration { get; init; } = new();
}
