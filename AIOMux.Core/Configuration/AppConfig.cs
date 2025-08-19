using System.Text.Json.Serialization;

namespace AIOMux.Core.Configuration;

/// <summary>
/// Represents the application configuration loaded from JSON
/// </summary>
public class AppConfig
{
    /// <summary>
    /// LLM configuration settings
    /// </summary>
    public LlmConfig LLM { get; set; } = new();

    /// <summary>
    /// Agent configuration settings
    /// </summary>
    public Dictionary<string, AgentConfig> Agents { get; set; } = new();

    /// <summary>
    /// Path settings for various application directories
    /// </summary>
    public PathConfig Paths { get; set; } = new();

    /// <summary>
    /// Validates the application configuration for global LLM and all agent configurations.
    /// </summary>
    /// <param name="errors">A list to hold validation error messages.</param>
    /// <returns>True if the configuration is valid; otherwise, false.</returns>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();
        List<string> llmErrors = new();
        if (LLM == null || !LLM.Validate(out llmErrors))
            errors.AddRange(llmErrors.Select(e => $"Global LLM: {e}"));
        if (Agents != null)
        {
            foreach (var kvp in Agents)
            {
                if (!kvp.Value.Validate(out var agentErrors))
                    errors.AddRange(agentErrors.Select(e => $"Agent '{kvp.Key}': {e}"));
            }
        }
        // Add more global validation as needed...
        return errors.Count == 0;
    }
}

/// <summary>
/// LLM configuration settings
/// </summary>
public class LlmConfig
{
    /// <summary>
    /// The provider to use (ollama, openai, azure, etc)
    /// </summary>
    public string Provider { get; set; } = "ollama";

    /// <summary>
    /// The default model to use
    /// </summary>
    public string DefaultModel { get; set; } = "llama3";

    /// <summary>
    /// API key for providers that require authentication
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint URL for the LLM provider
    /// </summary>
    public string EndpointUrl { get; set; } = "http://localhost:11434/api";

    /// <summary>
    /// Default temperature setting for generations
    /// </summary>
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;

    /// <summary>
    /// Maximum number of tokens to generate
    /// </summary>
    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; } = 2048;

    /// <summary>
    /// Maximum requests per minute (global rate limit for LLM requests)
    /// </summary>
    public int MaxRequestsPerMinute { get; set; } = 60;

    /// <summary>
    /// Additional provider-specific parameters
    /// </summary>
    public Dictionary<string, object> AdditionalParams { get; set; } = new();

    /// <summary>
    /// Validates the LLM configuration for required fields and valid endpoint URLs.
    /// </summary>
    /// <param name="errors">A list to hold validation error messages.</param>
    /// <returns>True if the configuration is valid; otherwise, false.</returns>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Provider))
            errors.Add("LLM provider is required.");
        if (string.IsNullOrWhiteSpace(DefaultModel))
            errors.Add("LLM model is required.");
        if (!string.IsNullOrWhiteSpace(EndpointUrl) && !Uri.IsWellFormedUriString(EndpointUrl, UriKind.Absolute))
            errors.Add($"EndpointUrl '{EndpointUrl}' is not a valid absolute URI.");
        // Example: If provider is SaaS, require ApiKey
        if ((Provider?.ToLowerInvariant() == "openai" || Provider?.ToLowerInvariant() == "azure") && string.IsNullOrWhiteSpace(ApiKey))
            errors.Add($"ApiKey is required for provider '{Provider}'.");
        // Add more validation as needed...
        return errors.Count == 0;
    }
}

/// <summary>
/// Agent configuration settings
/// </summary>
public class AgentConfig
{
    /// <summary>
    /// System prompt to use for the agent
    /// </summary>
    public string SystemPrompt { get; set; } = string.Empty;

    /// <summary>
    /// Additional configuration parameters for the agent
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Optional LLM configuration for this agent (overrides global)
    /// </summary>
    public LlmConfig? LLM { get; set; }

    /// <summary>
    /// Optional rate limit for this agent (requests per minute)
    /// </summary>
    public int? MaxRequestsPerMinute { get; set; }

    /// <summary>
    /// Validates the agent configuration for required fields and validates the LLM configuration if present.
    /// </summary>
    /// <param name="errors">A list to hold validation error messages.</param>
    /// <returns>True if the configuration is valid; otherwise, false.</returns>
    public bool Validate(out List<string> errors)
    {
        errors = new List<string>();
        if (string.IsNullOrWhiteSpace(SystemPrompt))
            errors.Add("SystemPrompt is required for agent.");
        if (LLM != null && !LLM.Validate(out var llmErrors))
            errors.AddRange(llmErrors.Select(e => $"LLM: {e}"));
        // Add more agent-specific validation as needed...
        return errors.Count == 0;
    }
}

/// <summary>
/// Path configuration settings
/// </summary>
public class PathConfig
{
    /// <summary>
    /// Path to the directory containing chain JSON files
    /// </summary>
    public string ChainsDirectory { get; set; } = "Chains";

    /// <summary>
    /// Path to the directory containing prompt templates
    /// </summary>
    public string PromptTemplatesDirectory { get; set; } = "PromptTemplates";

    /// <summary>
    /// Path to the directory for storing cached responses
    /// </summary>
    public string CacheDirectory { get; set; } = "Cache";
}