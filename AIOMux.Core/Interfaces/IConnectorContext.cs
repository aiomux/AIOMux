using AIOMux.Core.Models;

namespace AIOMux.Core.Interfaces;

/// <summary>
/// Context provided to a connector, used to publish events into the runtime.
/// </summary>
public interface IConnectorContext
{
    /// <summary>
    /// Connector-specific configuration from the solution manifest.
    /// </summary>
    IReadOnlyDictionary<string, string> Config { get; }

    /// <summary>
    /// Publishes a connector event for processing by the runtime.
    /// </summary>
    /// <param name="evt">The event to publish.</param>
    /// <param name="cancellationToken">Token used to cancel the publish operation.</param>
    Task PublishAsync(ConnectorEvent evt, CancellationToken cancellationToken = default);
}
