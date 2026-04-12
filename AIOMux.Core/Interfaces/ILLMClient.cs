namespace AIOMux.Core.Interfaces;

/// <summary>
/// Abstracts any Large-Language-Model backend (local or cloud).
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Identifies the LLM provider (for example: "ollama" or "openai").
    /// Used by the plugin loader to enforce curated-agent compatibility constraints.
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Model name this client is configured to use (for example: "llama3" or "gpt-4o").
    /// Used by the plugin loader to enforce curated-agent compatibility constraints.
    /// </summary>
    string Model { get; }

    /// <summary>
    /// Generate a completion for the provided prompt.
    /// </summary>
    Task<string> GenerateAsync(string prompt);

    /// <summary>
    /// Generate a completion for the provided user input and system prompt.
    /// </summary>
    Task<string> CompleteAsync(string userInput, string systemPrompt);
}
