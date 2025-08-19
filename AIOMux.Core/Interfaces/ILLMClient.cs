namespace AIOMux.Core.Interfaces;

/// <summary>
/// Abstracts any Large-Language-Model backend (local or cloud).
/// </summary>
public interface ILLMClient
{
    /// <summary>
    /// Generate a completion for the provided prompt.
    /// </summary>
    Task<string> GenerateAsync(string prompt);

    /// <summary>
    /// Generate a completion for the provided user input and system prompt.
    /// </summary>
    Task<string> CompleteAsync(string userInput, string systemPrompt);
}
