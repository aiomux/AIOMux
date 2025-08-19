using AIOMux.Core.Configuration;
using AIOMux.Core.Interfaces;
using System.Net.Http.Json;

namespace AIOMux.LLM;

/// <summary>
/// Client for interacting with the Ollama LLM API.
/// </summary>
public sealed class OllamaClient : ILLMClient
{
    private readonly HttpClient _http = new();
    private readonly string _model;
    private readonly RateLimiter _rateLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaClient"/> class.
    /// </summary>
    /// <param name="model">The model to use for the Ollama API.</param>
    /// <param name="maxRequestsPerMinute">The maximum number of requests allowed per minute.</param>
    public OllamaClient(string model = "llama3", int maxRequestsPerMinute = 60)
    {
        _model = model;
        _rateLimiter = new RateLimiter(maxRequestsPerMinute);
    }

    /// <summary>
    /// Generates a response from the Ollama API based on the provided prompt.
    /// </summary>
    /// <param name="prompt">The input prompt for the model.</param>
    /// <returns>The generated response as a string.</returns>
    public async Task<string> GenerateAsync(string prompt)
    {
        if (!_rateLimiter.TryRequest())
            return "[RATE LIMIT EXCEEDED] Please wait before making more requests.";

        var request = new { model = _model, prompt, stream = false };

        using var response = await _http.PostAsJsonAsync(
            "http://localhost:11434/api/generate", request);

        if (!response.IsSuccessStatusCode)
            return $"[OLLAMA ERROR] {response.StatusCode}";

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return json?["response"]?.ToString() ?? "[EMPTY]";
    }

    /// <summary>
    /// Sends a completion request to the Ollama API.
    /// </summary>
    /// <param name="userInput">The user input to complete.</param>
    /// <param name="systemPrompt">The system prompt to guide the completion.</param>
    /// <returns>The completed response as a string.</returns>
    public async Task<string> CompleteAsync(string userInput, string systemPrompt)
    {
        if (!_rateLimiter.TryRequest())
            return "[RATE LIMIT EXCEEDED] Please wait before making more requests.";

        var request = new { model = _model, prompt = $"{systemPrompt}\n\n{userInput}", stream = false };

        using var response = await _http.PostAsJsonAsync("http://localhost:11434/api/generate", request);
        if (!response.IsSuccessStatusCode)
            return $"[OLLAMA ERROR] {response.StatusCode}";

        var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return json?["response"]?.ToString() ?? "[EMPTY]";
    }
}
