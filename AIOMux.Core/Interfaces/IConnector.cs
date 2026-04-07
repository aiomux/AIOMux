namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract for a connector that can receive external events and publish them into the runtime.
/// </summary>
public interface IConnector
{
    /// <summary>
    /// Unique name used for connector lookup and identification.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Starts the connector and begins publishing events to the provided context.
    /// </summary>
    /// <param name="context">The context used to publish connector events into the runtime.</param>
    /// <param name="cancellationToken">Token used to signal that the connector should stop.</param>
    Task StartAsync(IConnectorContext context, CancellationToken cancellationToken = default);
}
