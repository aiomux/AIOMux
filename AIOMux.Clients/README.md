# AIOMux.Clients

AIOMux.Clients is a modern .NET library providing client implementations for interacting with Language Models in agent-based systems. It is designed to work seamlessly with [AIOMux.Core](../AIOMux.Core/), enabling you to integrate, orchestrate, and scale LLM-powered agents in your applications.

## Key Features
- **OllamaClient:** Easy integration with local Ollama LLM server
- **Rate limiting:** Prevents excessive requests to LLM endpoints
- **Extensible architecture:** Add new LLM clients as needed
- **Async API:** All LLM operations are asynchronous for scalability
- More clients coming soon (e.g., OpenAI, etc.)

## Getting Started

### Prerequisites
- .NET 8+
- Ollama server running locally (default: `http://localhost:11434`)

### Example: Basic Usage
```csharp
using AIOMux.Clients;

var llm = new OllamaClient(model: "llama3");
string response = await llm.GenerateAsync("What is the capital of France?");
Console.WriteLine(response);
```

### Example: Chat Loop
```csharp
using AIOMux.Clients;

var llm = new OllamaClient(model: "llama3.1");
while (true)
{
    Console.Write("You: ");
    string? input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.Trim().ToLower() == "exit")
        break;
    string response = await llm.GenerateAsync(input);
    Console.WriteLine($"AI: {response}");
}
```

## API Overview

### OllamaClient
- `OllamaClient(string model = "llama3", int maxRequestsPerMinute = 60)`
- `Task<string> GenerateAsync(string prompt)` — Generate a response from the LLM
- `Task<string> CompleteAsync(string userInput, string systemPrompt)` — Generate a completion with a system prompt

## Extending
You can add new LLM clients by implementing the `ILLMClient` interface from AIOMux.Core.

## License
This project is licensed under the MIT License. See [LICENSE](../LICENSE) for details.
