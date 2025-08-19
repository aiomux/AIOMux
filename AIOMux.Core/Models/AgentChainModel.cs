using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIOMux.Core.Models;

/// <summary>
/// Represents a serializable model of an agent chain with comprehensive validation.
/// </summary>
public class AgentChainModel
{
    /// <summary>
    /// Name of the chain.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of the chain.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// Steps in the chain.
    /// </summary>
    [JsonPropertyName("steps")]
    public List<AgentStep> Steps { get; set; } = new();

    /// <summary>
    /// Loads a chain model from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the JSON file containing the chain definition</param>
    /// <returns>The loaded chain model</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist</exception>
    /// <exception cref="JsonException">Thrown when the JSON is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when deserialization fails</exception>
    public static async Task<AgentChainModel> LoadFromFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Chain file not found: {filePath}");
        }

        try
        {
            using var fileStream = File.OpenRead(filePath);
            var model = await JsonSerializer.DeserializeAsync<AgentChainModel>(fileStream)
                       ?? throw new InvalidOperationException($"Failed to deserialize chain from {filePath}");

            // Validate the loaded model
            if (!model.Validate(out var errors))
            {
                throw new InvalidOperationException($"Loaded chain is invalid: {string.Join(", ", errors)}");
            }

            return model;
        }
        catch (JsonException ex)
        {
            throw new JsonException($"Invalid JSON in chain file {filePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves the chain model to a JSON file.
    /// </summary>
    /// <param name="filePath">Path where the JSON file will be saved</param>
    /// <exception cref="ArgumentException">Thrown when file path is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when the model is invalid</exception>
    public async Task SaveToFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        // Validate before saving
        if (!Validate(out var errors))
        {
            throw new InvalidOperationException($"Cannot save invalid chain: {string.Join(", ", errors)}");
        }

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using var fileStream = File.Create(filePath);
            await JsonSerializer.SerializeAsync(fileStream, this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save chain to {filePath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the chain model to ensure all steps are properly configured.
    /// </summary>
    /// <param name="errors">List of validation errors found</param>
    /// <returns>True if the chain is valid, false otherwise</returns>
    public bool Validate(out IList<string> errors)
    {
        errors = new List<string>();

        // Validate chain name
        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Chain name is required");
        }
        else if (Name.Length > 100)
        {
            errors.Add("Chain name cannot exceed 100 characters");
        }

        // Validate steps
        if (Steps.Count == 0)
        {
            errors.Add("Chain must have at least one step");
        }
        else if (Steps.Count > 50)
        {
            errors.Add("Chain cannot have more than 50 steps");
        }

        // Validate each step
        var outputKeys = new HashSet<string>();
        var inputKeys = new HashSet<string>();

        for (int i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            var stepPrefix = $"Step {i + 1}";

            // Validate agent name
            if (string.IsNullOrWhiteSpace(step.AgentName))
            {
                errors.Add($"{stepPrefix}: Agent name is required");
            }
            else if (step.AgentName.Length > 50)
            {
                errors.Add($"{stepPrefix}: Agent name cannot exceed 50 characters");
            }

            // Validate OutputTo
            if (!string.IsNullOrEmpty(step.OutputTo))
            {
                if (outputKeys.Contains(step.OutputTo))
                {
                    errors.Add($"{stepPrefix}: OutputTo key '{step.OutputTo}' is already used by another step");
                }
                else
                {
                    outputKeys.Add(step.OutputTo);
                }
            }
            else
            {
                // Default OutputTo is agent name
                var defaultOutputKey = step.AgentName;
                if (outputKeys.Contains(defaultOutputKey))
                {
                    errors.Add($"{stepPrefix}: Default OutputTo key '{defaultOutputKey}' conflicts with another step");
                }
                else
                {
                    outputKeys.Add(defaultOutputKey);
                }
            }

            // Track InputFrom for dependency validation
            if (!string.IsNullOrEmpty(step.InputFrom) && step.InputFrom != "user")
            {
                inputKeys.Add(step.InputFrom);
            }
        }

        // Validate input dependencies
        foreach (var inputKey in inputKeys)
        {
            if (!outputKeys.Contains(inputKey))
            {
                errors.Add($"InputFrom '{inputKey}' references an output that doesn't exist");
            }
        }

        // Check for circular dependencies (simplified check)
        if (HasCircularDependencies())
        {
            errors.Add("Chain contains circular dependencies");
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// Checks for circular dependencies in the chain steps.
    /// </summary>
    /// <returns>True if circular dependencies are found, false otherwise</returns>
    private bool HasCircularDependencies()
    {
        var graph = new Dictionary<string, List<string>>();
        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();

        // Build dependency graph
        foreach (var step in Steps)
        {
            var outputKey = step.OutputTo ?? step.AgentName;
            if (!graph.ContainsKey(outputKey))
            {
                graph[outputKey] = new List<string>();
            }

            if (!string.IsNullOrEmpty(step.InputFrom) && step.InputFrom != "user")
            {
                if (!graph.ContainsKey(step.InputFrom))
                {
                    graph[step.InputFrom] = new List<string>();
                }
                graph[step.InputFrom].Add(outputKey);
            }
        }

        // Check for cycles using DFS
        foreach (var node in graph.Keys)
        {
            if (HasCycleDFS(node, graph, visited, recursionStack))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Performs DFS to detect cycles in the dependency graph.
    /// </summary>
    private bool HasCycleDFS(string node, Dictionary<string, List<string>> graph,
        HashSet<string> visited, HashSet<string> recursionStack)
    {
        if (recursionStack.Contains(node))
        {
            return true;
        }

        if (visited.Contains(node))
        {
            return false;
        }

        visited.Add(node);
        recursionStack.Add(node);

        if (graph.ContainsKey(node))
        {
            foreach (var neighbor in graph[node])
            {
                if (HasCycleDFS(neighbor, graph, visited, recursionStack))
                {
                    return true;
                }
            }
        }

        recursionStack.Remove(node);
        return false;
    }

    /// <summary>
    /// Creates a copy of this chain model.
    /// </summary>
    /// <returns>A deep copy of the chain model</returns>
    public AgentChainModel Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<AgentChainModel>(json)
               ?? throw new InvalidOperationException("Failed to clone chain model");
    }

    /// <summary>
    /// Gets a summary of the chain for logging/display purposes.
    /// </summary>
    /// <returns>A formatted summary string</returns>
    public string GetSummary()
    {
        var summary = $"Chain: {Name}";
        if (!string.IsNullOrEmpty(Description))
        {
            summary += $" - {Description}";
        }
        summary += $" ({Steps.Count} steps)";
        return summary;
    }
}