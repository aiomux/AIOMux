namespace AIOMux.Core.Models;

/// <summary>
/// Represents an event emitted by a connector and published into the runtime.
/// </summary>
public sealed class ConnectorEvent
{
    /// <summary>
    /// Name of the connector that produced this event.
    /// </summary>
    public string ConnectorName { get; set; } = string.Empty;

    /// <summary>
    /// Identifies the kind of event (e.g., "message", "trigger", "webhook").
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Optional event payload. The connector and consumer agree on the shape.
    /// </summary>
    public object? Payload { get; set; }

    /// <summary>
    /// Optional name of the agent that should handle this event.
    /// </summary>
    public string? EntryAgent { get; set; }

    /// <summary>
    /// Additional metadata associated with the event.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
