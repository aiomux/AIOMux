namespace AIOMux.Local.Config;

/// <summary>
/// Model configuration for specifying the LLM provider and connection details.
/// </summary>
public class ModelConfig
{
    /// <summary>
    /// The LLM provider: "ollama" or "openai"
    /// </summary>
    public string Provider { get; set; } = "ollama";

    /// <summary>
    /// The base URL for the LLM service (e.g., http://localhost:11434 for Ollama)
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Environment variable name containing the API key (e.g., OPENAI_API_KEY)
    /// </summary>
    public string? ApiKeyEnvVar { get; set; }

    /// <summary>
    /// Model name to use (e.g., "llama3" for Ollama, "gpt-4" for OpenAI)
    /// </summary>
    public string ModelName { get; set; } = "llama3";
}
