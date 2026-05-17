using AIOMux.Core.Policy;

namespace AIOMux.Core.Models;

/// <summary>
/// Creates safe, minimal tool descriptors derived from existing tool contracts.
/// </summary>
public static class ToolDescriptorFactory
{
    /// <summary>
    /// Creates a fallback descriptor from tool name and supported operations.
    /// </summary>
    public static ToolDescriptor CreateFallback(string name, IReadOnlyCollection<ToolOperation>? supportedOperations)
    {
        var safeName = string.IsNullOrWhiteSpace(name) ? "unknown" : name;
        var operations = supportedOperations ?? [];

        return new ToolDescriptor
        {
            Name = safeName,
            Description = $"Auto-generated descriptor for tool '{safeName}'.",
            Version = "1.0.0",
            Category = "general",
            Operations = operations
                .Distinct()
                .Select(CreateOperationDescriptor)
                .ToList()
        };
    }

    private static ToolOperationDescriptor CreateOperationDescriptor(ToolOperation operation)
    {
        var operationName = operation.ToString();
        var requestType = typeof(string);
        var responseType = typeof(string);

        return new ToolOperationDescriptor
        {
            Name = operationName,
            Description = $"Operation '{operationName}' supported by this tool.",
            Operation = operation,
            RequestType = requestType,
            ResponseType = responseType,
            RequestSchemaJson = ToolDescriptorMetadataSerializer.TryCreateSchemaJson(requestType),
            ResponseSchemaJson = ToolDescriptorMetadataSerializer.TryCreateSchemaJson(responseType),
            IsDangerous = IsDangerous(operation),
            IsReadOnly = IsReadOnly(operation),
            Tags = [operationName.ToLowerInvariant()],
            ExampleJson = null,
            Constraints = []
        };
    }

    private static bool IsDangerous(ToolOperation operation) => operation is
        ToolOperation.Delete or
        ToolOperation.Write or
        ToolOperation.HttpPost or
        ToolOperation.CommandExecute or
        ToolOperation.ProcessStart or
        ToolOperation.ProcessKill or
        ToolOperation.ServiceStop or
        ToolOperation.ServiceRestart or
        ToolOperation.RegistryWrite or
        ToolOperation.SecretRead or
        ToolOperation.DbWrite;

    private static bool IsReadOnly(ToolOperation operation) => operation is
        ToolOperation.Read or
        ToolOperation.DbRead;
}
