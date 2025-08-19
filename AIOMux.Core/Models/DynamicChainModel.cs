using System.Text.Json;

namespace AIOMux.Core.Models;

/// <summary>
/// Represents a dynamically generated chain model from planner agent output.
/// </summary>
public class DynamicChainModel
{
    /// <summary>
    /// Parse a JSON string into an AgentChainModel.
    /// </summary>
    /// <param name="json">JSON representing steps in a chain</param>
    /// <param name="chainName">Optional name for the chain</param>
    /// <returns>An AgentChainModel instance</returns>
    public static AgentChainModel Parse(string json, string chainName = "DynamicChain")
    {
        try
        {
            // First, try to fix any property name mismatches in the JSON
            json = MapAgentPropertyToAgentName(json);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var steps = JsonSerializer.Deserialize<List<AgentStep>>(json, options) ?? [];

            // Validate steps after deserialization
            for (int i = 0; i < steps.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(steps[i].AgentName))
                {
                    throw new ArgumentException($"Step {i + 1}: Agent name cannot be empty");
                }
            }

            return new AgentChainModel
            {
                Name = chainName,
                Description = "Dynamically generated chain",
                Steps = steps
            };
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Failed to parse dynamic chain: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Maps the "agent" property to "AgentName" in the JSON string if needed
    /// </summary>
    private static string MapAgentPropertyToAgentName(string json)
    {
        try
        {
            // Parse the JSON to check if it uses "agent" instead of "agentName"
            var document = JsonDocument.Parse(json);
            bool needsPropertyMapping = false;

            // Check if any steps use "agent" property instead of "agentName"
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.TryGetProperty("agent", out _) &&
                    !element.TryGetProperty("agentName", out _))
                {
                    needsPropertyMapping = true;
                    break;
                }
            }

            // If we need to map properties, do the replacement
            if (needsPropertyMapping)
            {
                return json.Replace("\"agent\":", "\"agentName\":");
            }

            return json;
        }
        catch
        {
            // If there's an error parsing the JSON at this stage,
            // return the original JSON and let the main parser handle it
            return json;
        }
    }
}