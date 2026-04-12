using AIOMux.Core.Configuration;
using AIOMux.Core.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIOMux.Clients;

/// <summary>
/// Client for interacting with the OpenAI Chat Completions API.
/// </summary>
public sealed class OpenAIClient : ILLMClient
{
    private readonly HttpClient _http = new();
    private readonly string _model;
    private readonly RateLimiter _rateLimiter;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIClient"/> class.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <param name="model">Model name (for example: gpt-4o-mini).</param>
    /// <param name="baseUrl">Base URL for the OpenAI API. Defaults to https://api.openai.com/v1.</param>
    /// <param name="maxRequestsPerMinute">Maximum requests per minute for local rate limiting.</param>
    public OpenAIClient(string apiKey, string model = "gpt-4o-mini", string? baseUrl = null, int maxRequestsPerMinute = 60)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("OpenAI API key cannot be null or empty.", nameof(apiKey));

        _model = model;
        _rateLimiter = new RateLimiter(maxRequestsPerMinute);
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1" : baseUrl.TrimEnd('/');

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <inheritdoc/>
    public string Provider => "openai";

    /// <inheritdoc/>
    public string Model => _model;

    /// <inheritdoc/>
    public Task<string> GenerateAsync(string prompt)
        => CompleteAsync(prompt, "You are a helpful assistant.");

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(string userInput, string systemPrompt)
    {
        if (!_rateLimiter.TryRequest())
            return "[RATE LIMIT EXCEEDED] Please wait before making more requests.";

        var requestBody = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userInput }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync($"{_baseUrl}/chat/completions", content);

        if (!response.IsSuccessStatusCode)
            return $"[OPENAI ERROR] {response.StatusCode}";

        using var stream = await response.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);

        if (doc.RootElement.TryGetProperty("choices", out var choices)
            && choices.ValueKind == JsonValueKind.Array
            && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var contentElement)
                && contentElement.ValueKind == JsonValueKind.String)
            {
                return contentElement.GetString() ?? "[EMPTY]";
            }
        }

        return "[EMPTY]";
    }
}
