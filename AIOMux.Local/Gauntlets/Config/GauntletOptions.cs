using AIOMux.Clients;
using AIOMux.Core.Interfaces;
using AIOMux.Local.Config;

namespace AIOMux.Local.Gauntlets.Config;

/// <summary>
/// Parsed options for a gauntlet run, with defaults sourced from aiomux.json.
/// </summary>
internal sealed class GauntletOptions
{
    /// <summary>Run with a real LLM agent and real tools instead of fixtures.</summary>
    public bool Live { get; init; }

    public string Provider { get; init; } = "ollama";
    public string Model { get; init; } = "llama3.1";
    public string? ApiKey { get; init; }
    public string? BaseUrl { get; init; }

    public string Query { get; init; } = "summarize credential notes";
    public int TopK { get; init; } = 1;

    /// <summary>
    /// Parses gauntlet flags from <paramref name="args"/> starting at <paramref name="startIndex"/>.
    /// Falls back to <paramref name="config"/> for provider / model / base-url.
    /// </summary>
    public static GauntletOptions Parse(string[] args, int startIndex, AiomuxConfig? config = null)
    {
        var live = false;
        var provider = config?.Model.Provider ?? "ollama";
        var model = config?.Model.ModelName ?? "llama3.1";
        string? apiKey = null;
        string? baseUrl = config?.Model.BaseUrl;
        var query = "summarize credential notes";
        var topK = 1;

        for (var i = startIndex; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--live":
                    live = true;
                    break;
                case "--provider" when i + 1 < args.Length:
                    provider = args[++i];
                    break;
                case "--model" when i + 1 < args.Length:
                    model = args[++i];
                    break;
                case "--apikey" when i + 1 < args.Length:
                    apiKey = args[++i];
                    break;
                case "--base-url" when i + 1 < args.Length:
                    baseUrl = args[++i];
                    break;
                case "--q" when i + 1 < args.Length:
                    query = args[++i];
                    break;
                case "--topk" when i + 1 < args.Length && int.TryParse(args[i + 1], out var k):
                    topK = k;
                    i++;
                    break;
            }
        }

        // Resolve API key from the configured env-var when not passed directly.
        if (apiKey == null && config?.Model.ApiKeyEnvVar != null)
        {
            apiKey = Environment.GetEnvironmentVariable(config.Model.ApiKeyEnvVar);
        }

        return new GauntletOptions
        {
            Live = live,
            Provider = provider,
            Model = model,
            ApiKey = apiKey,
            BaseUrl = baseUrl,
            Query = query,
            TopK = topK
        };
    }

    /// <summary>
    /// Creates the <see cref="ILLMClient"/> described by these options.
    /// </summary>
    public ILLMClient CreateClient()
    {
        if (Provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                throw new InvalidOperationException(
                    "OpenAI provider requires --apikey or the configured API key env-var.");
            }

            return new OpenAIClient(ApiKey, Model, BaseUrl);
        }

        // Default: Ollama
        var baseUrl = string.IsNullOrWhiteSpace(BaseUrl) ? null : BaseUrl;
        var client = new OllamaClient(Model);

        // If a custom base URL was supplied, we note it (OllamaClient uses localhost by default).
        if (baseUrl != null)
        {
            Console.WriteLine($"  [gauntlet] Warning: --base-url is set but OllamaClient uses its own endpoint. " +
                              $"To override, extend OllamaClient to accept a base URL.");
        }

        return client;
    }
}
