namespace AIOMux.Core.Models;

/// <summary>
/// Structured metadata that describes a tool and its operations.
/// </summary>
public sealed class ToolDescriptor
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the descriptor version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets the tool category.
    /// </summary>
    public string Category { get; set; } = "general";

    /// <summary>
    /// Gets or sets operation descriptors exposed by this tool.
    /// </summary>
    public List<ToolOperationDescriptor> Operations { get; set; } = new();
}

/// <summary>
/// Structured metadata that describes one operation exposed by a tool.
/// </summary>
public sealed class ToolOperationDescriptor
{
    /// <summary>
    /// Gets or sets the operation name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the operation description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the policy operation represented by this descriptor.
    /// </summary>
    public Policy.ToolOperation Operation { get; set; }

    /// <summary>
    /// Gets the CLR request payload type when known.
    /// </summary>
    public Type? RequestType { get; init; }

    /// <summary>
    /// Gets the CLR response payload type when known.
    /// </summary>
    public Type? ResponseType { get; init; }

    /// <summary>
    /// Gets optional JSON schema metadata for the request payload.
    /// </summary>
    public string? RequestSchemaJson { get; init; }

    /// <summary>
    /// Gets optional JSON schema metadata for the response payload.
    /// </summary>
    public string? ResponseSchemaJson { get; init; }

    /// <summary>
    /// Gets or sets whether this operation is considered dangerous.
    /// </summary>
    public bool IsDangerous { get; set; }

    /// <summary>
    /// Gets or sets whether this operation is read-only.
    /// </summary>
    public bool IsReadOnly { get; set; }

    /// <summary>
    /// Gets or sets optional tags used by external planners and UIs.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets an example JSON payload for this operation.
    /// </summary>
    public string? ExampleJson { get; set; }

    /// <summary>
    /// Gets or sets optional constraints associated with this operation.
    /// </summary>
    public List<ToolConstraintDescriptor> Constraints { get; set; } = new();
}

/// <summary>
/// Optional metadata describing a single operation constraint.
/// </summary>
public sealed class ToolConstraintDescriptor
{
    /// <summary>
    /// Gets or sets the constraint name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the constraint description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the constraint type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the constraint is required.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default value for the constraint.
    /// </summary>
    public object? DefaultValue { get; set; }
}
