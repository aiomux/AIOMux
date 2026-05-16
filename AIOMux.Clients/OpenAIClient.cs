using AIOMux.Core.Configuration;
using AIOMux.Core.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIOMux.Clients;

/// <summary>
/// Client for interacting with the OpenAI Chat Completions API.
/// </summary>
public sealed class OpenAIClient : ILLMClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _model;
    private readonly RateLimiter _rateLimiter;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIClient"/> class.
    /// Creates and manages its own <see cref="HttpClient"/> instance.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <param name="model">Model name (for example: gpt-4o-mini).</param>
    /// <param name="baseUrl">Base URL for the OpenAI API. Defaults to https://api.openai.com/v1.</param>
    /// <param name="maxRequestsPerMinute">Maximum requests per minute for local rate limiting.</param>
    public OpenAIClient(string apiKey, string model = "gpt-4o-mini", string? baseUrl = null, int maxRequestsPerMinute = 60)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("OpenAI API key cannot be null or empty.", nameof(apiKey));

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _ownsHttp = true;
        _model = model;
        _rateLimiter = new RateLimiter(maxRequestsPerMinute);
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1" : baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIClient"/> class with an injected <see cref="HttpClient"/>.
    /// The caller is responsible for the lifetime of the <see cref="HttpClient"/> and for setting any required authorization headers.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> to use for requests.</param>
    /// <param name="model">Model name (for example: gpt-4o-mini).</param>
    /// <param name="baseUrl">Base URL for the OpenAI API. Defaults to https://api.openai.com/v1.</param>
    /// <param name="maxRequestsPerMinute">Maximum requests per minute for local rate limiting.</param>
    public OpenAIClient(HttpClient http, string model = "gpt-4o-mini", string? baseUrl = null, int maxRequestsPerMinute = 60)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsHttp = false;
        _model = model;
        _rateLimiter = new RateLimiter(maxRequestsPerMinute);
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.openai.com/v1" : baseUrl.TrimEnd('/');
    }

    /// <inheritdoc/>
    public string Provider => "openai";

    /// <inheritdoc/>
    public string Model => _model;

    /// <inheritdoc/>
    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
        => CompleteAsync(prompt, "You are a helpful assistant.", cancellationToken);

    /// <inheritdoc/>
    public async Task<string> CompleteAsync(string userInput, string systemPrompt, CancellationToken cancellationToken = default)
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
        using var response = await _http.PostAsync($"{_baseUrl}/chat/completions", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
            return $"[OPENAI ERROR] {response.StatusCode}";

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

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

    /// <summary>
    /// Disposes resources used by this client.
    /// Only disposes the <see cref="HttpClient"/> if this instance created it.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}
