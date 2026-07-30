# AIOMux.Clients

AIOMux.Clients provides .NET 10 client implementations for Large Language Models (LLMs), with built-in support for:

- Ollama (local and self-hosted)
- OpenAI Chat Completions API

The package is designed for agent-driven workflows powered by AIOMux.Core and exposes a shared `ILLMClient` interface for consistent integration across providers.

## Features

- Unified `ILLMClient` contract across providers
- Provider metadata via `Provider` and `Model`
- Async APIs with cancellation token support
- Optional `HttpClient` injection for DI and lifecycle control
- Basic per-client rate limiting
- Clean fallback messages for API and rate limit errors

## Installation

```bash
dotnet add package AIOMux.Clients
```

## Quick start

### Ollama

```csharp
using AIOMux.Clients;

using var client = new OllamaClient(
	model: "llama3",
	maxRequestsPerMinute: 60,
	endpoint: "http://localhost:11434");

var text = await client.GenerateAsync("Summarize what AIOMux does in one sentence.");
Console.WriteLine(text);
```

### OpenAI

```csharp
using AIOMux.Clients;

using var client = new OpenAIClient(
	apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
	model: "gpt-4o-mini",
	maxRequestsPerMinute: 60);

var text = await client.CompleteAsync(
	userInput: "Write a short welcome message for new users.",
	systemPrompt: "You are a concise assistant.");

Console.WriteLine(text);
```

## API surface

Both providers implement:

- `Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken = default)`
- `Task<string> CompleteAsync(string userInput, string systemPrompt, CancellationToken cancellationToken = default)`
- `string Provider`
- `string Model`

## Notes

- `OllamaClient` defaults to `http://localhost:11434` and `llama3`.
- `OpenAIClient` defaults to `https://api.openai.com/v1` and `gpt-4o-mini`.
- If you inject `HttpClient`, your code owns its lifetime and authorization setup.

## Repository

- Source: https://github.com/aiomux/AIOMux/tree/master/AIOMux.Clients
- Website: https://aiomux.ai
