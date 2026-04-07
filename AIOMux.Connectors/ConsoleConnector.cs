using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Connectors.Console;

/// <summary>
/// A minimal connector that reads lines from standard input and publishes each non-empty
/// line as a connector event. Intended to validate end-to-end serve mode execution.
/// </summary>
public sealed class ConsoleConnector : IConnector
{
    /// <inheritdoc />
    public string Name => "console";

    /// <inheritdoc />
    public async Task StartAsync(IConnectorContext context, CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await Task.Run(static () => System.Console.ReadLine()).WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            await context.PublishAsync(new ConnectorEvent
            {
                ConnectorName = "console",
                EventType = "console.line",
                Payload = line
            }, cancellationToken);
        }
    }
}
