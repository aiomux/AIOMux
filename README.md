# AIOMux

AIOMux is a modular .NET solution for building, orchestrating, and extending AI-powered agents and tools. It is designed for flexibility, plugin support, and integration with large language models (LLMs).

## Projects

### AIOMux.Core
- Core agent orchestration and management
- Agent plugin architecture
- Agent chains and context management
- Interfaces for agents, plugins, and tools

### AIOMux.LLM
- LLM client implementations (e.g., Ollama)
- Abstraction for integrating various LLM providers

## Features
- Agent plugin system for extensibility
- Chainable agent workflows
- LLM-powered code/documentation review
- Resume review tool (PDF/text)
- XML documentation standards
- .NET 8 compatible

## Architecture
- **AIOMux.Core**: Handles agent registration, chaining, context, and plugin loading.
- **AIOMux.LLM**: Provides LLM client implementations and abstractions for agent interaction.

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Build & Run
```sh
dotnet build
```

```

## Extending
- Implement `IAgent`, `IAgentPlugin`, or `ITool` for new agents/tools
- Add LLM clients by implementing `ILLMClient`
- Use `AgentManager` to register and load plugins

## Contributing
Pull requests and issues are welcome! Please follow standard .NET coding and documentation conventions.

## License
MIT

---
For more details, see the source code and XML documentation comments throughout the projects.
