using AIOMux.Core.Configuration;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using System.Net.Http.Json;

namespace AIOMux.Clients;

/// <summary>
/// Client for interacting with the Ollama LLM API.
/// </summary>
public sealed class OllamaClient : ILLMClient, IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly string _model;
    private readonly RateLimiter _rateLimiter;
    private readonly string _generateEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaClient"/> class.
    /// Creates and manages its own <see cref="HttpClient"/> instance.
    /// </summary>
    /// <param name="model">The model to use for the Ollama API.</param>
    /// <param name="maxRequestsPerMinute">The maximum number of requests allowed per minute.</param>
    /// <param name="endpoint">The Ollama server endpoint base URL.</param>
    public OllamaClient(string model = "llama3", int maxRequestsPerMinute = 60, string endpoint = "http://localhost:11434")
    {
        _http = new HttpClient();
        _ownsHttp = true;
        _model = model;
        _rateLimiter = new RateLimiter(maxRequestsPerMinute);
        _generateEndpoint = $"{endpoint.TrimEnd('/')}/api/generate";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaClient"/> class with an injected <see cref="HttpClient"/>.
    /// The caller is responsible for the lifetime of the <see cref="HttpClient"/>.
    /// </summary>
    /// <param name="http">The <see cref="HttpClient"/> to use for requests.</param>
    /// <param name="model">The model to use for the Ollama API.</param>
    /// <param name="maxRequestsPerMinute">The maximum number of requests allowed per minute.</param>
    /// <param name="endpoint">The Ollama server endpoint base URL.</param>
    public OllamaClient(HttpClient http, string model = "llama3", int maxRequestsPerMinute = 60, string endpoint = "http://localhost:11434")
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsHttp = false;
        _model = model;
        _rateLimiter = new RateLimiter(maxRequestsPerMinute);
        _generateEndpoint = $"{endpoint.TrimEnd('/')}/api/generate";
    }

    /// <summary>
    /// Generates a response from the Ollama API based on the provided prompt.
    /// </summary>
    /// <param name="prompt">The input prompt for the model.</param>
    /// <returns>The generated response as a string.</returns>
    public async Task<LlmResult> GenerateAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryRequest())
        {
            return new LlmResult
            {
                Success = false,
                ErrorCode = "RATE_LIMIT_EXCEEDED",
                ErrorMessage = "Please wait before making more requests."
            };
        }

        try
        {
            var request = new { model = _model, prompt, stream = false };

            using var response = await _http.PostAsJsonAsync(_generateEndpoint, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new LlmResult
                {
                    Success = false,
                    ErrorCode = "TRANSPORT_ERROR",
                    ErrorMessage = $"Ollama returned HTTP {(int)response.StatusCode} ({response.StatusCode})."
                };
            }

            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken);
            var content = json != null && json.TryGetValue("response", out var value)
                ? value?.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(content))
            {
                return new LlmResult
                {
                    Success = false,
                    ErrorCode = "EMPTY_RESPONSE",
                    ErrorMessage = "The model returned empty content."
                };
            }

            return new LlmResult
            {
                Success = true,
                Content = content
            };
        }
        catch (Exception ex)
        {
            return new LlmResult
            {
                Success = false,
                ErrorCode = "TRANSPORT_ERROR",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Sends a completion request to the Ollama API.
    /// </summary>
    /// <param name="userInput">The user input to complete.</param>
    /// <param name="systemPrompt">The system prompt to guide the completion.</param>
    /// <returns>The completed response as a string.</returns>
    public async Task<LlmResult> CompleteAsync(string userInput, string systemPrompt, CancellationToken cancellationToken = default)
    {
        if (!_rateLimiter.TryRequest())
        {
            return new LlmResult
            {
                Success = false,
                ErrorCode = "RATE_LIMIT_EXCEEDED",
                ErrorMessage = "Please wait before making more requests."
            };
        }

        try
        {
            var request = new { model = _model, prompt = $"{systemPrompt}\n\n{userInput}", stream = false };

            using var response = await _http.PostAsJsonAsync(_generateEndpoint, request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new LlmResult
                {
                    Success = false,
                    ErrorCode = "TRANSPORT_ERROR",
                    ErrorMessage = $"Ollama returned HTTP {(int)response.StatusCode} ({response.StatusCode})."
                };
            }

            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken);
            var content = json != null && json.TryGetValue("response", out var value)
                ? value?.ToString()
                : null;

            if (string.IsNullOrWhiteSpace(content))
            {
                return new LlmResult
                {
                    Success = false,
                    ErrorCode = "EMPTY_RESPONSE",
                    ErrorMessage = "The model returned empty content."
                };
            }

            return new LlmResult
            {
                Success = true,
                Content = content
            };
        }
        catch (Exception ex)
        {
            return new LlmResult
            {
                Success = false,
                ErrorCode = "TRANSPORT_ERROR",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public string Provider => "ollama";

    /// <inheritdoc />
    public string Model => _model;

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
