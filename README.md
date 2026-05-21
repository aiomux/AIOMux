# AIOMux - AI Operations Multiplexer

Build reliable AI workflows you can actually ship.

AIOMux is a .NET runtime for composing, executing, and governing multi-agent AI systems. It is local-first by design: run fully on-device with [Ollama](https://ollama.com), then add hosted providers such as OpenAI only when you choose. Agents, tools, LLM profiles, and connectors are declared in `solution.json` and executed through a straightforward CLI.

If you want the speed of agentic workflows without giving up control, auditability, or policy enforcement, AIOMux is built for that.

---

## Features

- **Multi-agent execution plans**: Declare sequential or structured agent pipelines in JSON.
- **Local-first LLM execution**: Run fully on-device against [Ollama](https://ollama.com) models with no cloud dependency; also supports OpenAI and any custom provider via `ILLMClient`.
- **Named LLM profiles**: Bind different providers and models per agent using named profiles in the solution manifest.
- **Tool dispatch with policy enforcement**: Agents invoke tools through a typed dispatcher, and every call is evaluated against a configurable operation policy.
- **Connectors**: Attach real-time event sources (for example, console or custom connectors) to drive solutions in serve mode.
- **Replay support**: Record tool execution results and replay them (full or tools-only) for deterministic testing.
- **Fork replay**: Branch from a recorded run and re-execute from a given step with different inputs.
- **Execution modes**: `Development` (AllowAll policy permitted) and `Production` (unsafe configurations rejected at startup).
- **Solution validation**: Run pre-flight schema and semantic checks before any agent runs.

### Why AIOMux is worth trying now

- **Prototype fast, harden safely**: start in Development mode, then move to Production mode with stricter policy checks.
- **Control operational risk**: tool calls are operation-gated before execution, not just logged after the fact.
- **Debug without guesswork**: replay and fork capabilities make incident analysis and iteration much faster.
- **Keep architecture open**: agents, tools, connectors, and LLM clients are all extensible via interfaces.

---

## Project Structure

| Project | Description |
|---|---|
| `AIOMux.Core` | Core abstractions: agents, tools, execution runtime, policy engine, replay, builders |
| `AIOMux.Local` | CLI host: `SolutionRunner`, `SolutionLoader`, `SolutionDefinition`, command dispatch |
| `AIOMux.Clients` | Built-in LLM clients: `OllamaClient`, `OpenAIClient` |
| `AIOMux.Connectors` | Built-in connectors: `ConsoleConnector` |
| `AIOMux.Tests` | Unit and integration tests |

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- An LLM endpoint (Ollama or OpenAI) if your agents require one

### Build

```bash
dotnet build
```

### Try it in under 2 minutes

```bash
dotnet run --project AIOMux.Local -- validate path/to/solution.json
dotnet run --project AIOMux.Local -- run path/to/solution.json "hello"
```

### Run a Solution

```bash
dotnet run --project AIOMux.Local -- run path/to/solution.json "your input here"
```

### Serve Mode (continuous event loop)

```bash
dotnet run --project AIOMux.Local -- serve path/to/solution.json
```

Press `Ctrl+C` to stop.

### Validate a Solution

```bash
dotnet run --project AIOMux.Local -- validate path/to/solution.json
```

### Replay a Previous Run

```bash
dotnet run --project AIOMux.Local -- replay path/to/solution.json <run-id>
```

### Fork from a Recorded Run

```bash
dotnet run --project AIOMux.Local -- fork path/to/solution.json <run-id> <step-index> "new input"
```

---

## Solution Manifest (`solution.json`)

A solution manifest ties together the entry plan, agents, tools, connectors, LLM profiles, and policy:

```json
{
  "name": "My Solution",
  "description": "Example multi-agent pipeline",
  "mode": "Development",
  "entry": "plans/main.json",
  "policyConfig": "policy.json",
  "agents": [
	"agents/MyAgent.dll"
  ],
  "tools": [
	"tools/MyTools.dll"
  ],
  "connectors": [],
  "connectorConfigurations": [],
  "llmProfiles": {
	"default": {
	  "provider": "ollama",
	  "endpoint": "http://localhost:11434",
	  "model": "llama3",
	  "maxRequestsPerMinute": 60
	},
	"reasoning": {
	  "provider": "openai",
	  "model": "gpt-4o",
	  "apiKeyEnvironmentVariable": "OPENAI_API_KEY"
	}
  },
  "replay": {
	"enabled": true,
	"storagePath": "runs"
  }
}
```

### Key Fields

| Field | Description |
|---|---|
| `mode` | `Development` or `Production`. Production rejects AllowAll policy. |
| `entry` | Path to the execution plan JSON, relative to `solution.json`. |
| `policyConfig` | Required. Path to the operation policy file. |
| `agents` | Paths to agent package DLLs. Each is scanned for `IAgent` implementations. |
| `tools` | Paths to tool package DLLs. Each is scanned for `ITool` implementations. |
| `connectors` | Paths to connector package DLLs. |
| `connectorConfigurations` | Runtime connector instances to start in serve mode. |
| `llmProfiles` | Named LLM configurations. The `"default"` key is used when no profile is specified. |
| `entryAgent` | Overrides the default entry agent for connector-sourced events. |

---

## Execution Plans

Plans are declared in JSON and reference agent steps by name:

```json
{
  "name": "Main Plan",
  "steps": [
	{
	  "agent": "MyAgent",
	  "input": "{{input}}"
	},
	{
	  "agent": "SummaryAgent",
	  "input": "{{steps[0].output}}"
	}
  ]
}
```

Plans can be sourced statically from a file (`PlanSource.Static`) or generated dynamically at runtime.

---

## Policy Configuration (`policy.json`)

Every tool call is validated against the operation policy before execution. Tools absent from the policy are denied by default.

```json
{
  "version": "1",
  "tools": {
	"file": {
	  "allowedOperations": ["Read"],
	  "constraints": {
		"Read": {
		  "allowedPaths": ["C:/apps/data"]
		}
	  }
	},
	"shell": {
	  "allowedOperations": ["CommandExecute"],
	  "constraints": {
		"CommandExecute": {
		  "allowedCommands": ["echo", "ls"]
		}
	  }
	}
  }
}
```

---

## Implementing an Agent

Implement `IAgent` in a class library, build it to a DLL, and reference the path in `solution.agents`:

```csharp
public sealed class GreeterAgent : IAgent
{
	public string Name => "Greeter";
	public string Description => "Returns a greeting for any input.";

	public AgentMetadata Metadata => new()
	{
		Name = Name,
		Description = Description,
		RequiredLlmProfiles = ["default"]
	};

	public async Task<StepExecutionResult> ExecuteAsync(ExecutionContext context, CancellationToken cancellationToken = default)
	{
		var llm = context.Services.LlmResolver?.Resolve("default")
			?? throw new InvalidOperationException("No default LLM profile.");

		var response = await llm.GenerateAsync($"Greet: {context.Input}", cancellationToken);
		return StepExecutionResult.Success(response);
	}
}
```

Use `AgentMetadata.RequiredLlmProfiles` to declare which named LLM profiles your agent needs. The runtime validates their presence before agent construction when a resolver is provided.

---

## Implementing a Tool

Inherit from `DispatchableToolBase`.

Tool execution in AIOMux is operation-aware and policy-gated:

- `SupportedOperations` declares the maximum set of `ToolOperation` categories the tool can perform.
- `Analyze(string input)` reports which operations the current invocation actually requests.

The dispatcher evaluates policy against those requested operations before invoking the tool.

```csharp
public sealed class EchoTool : DispatchableToolBase
{
	public override string Name => "echo";

	public override IReadOnlyCollection<ToolOperation> SupportedOperations =>
		[ToolOperation.Read];

	public override ToolExecutionAnalysis Analyze(string input) =>
		new() { RequestedOperations = [ToolOperation.Read] };

	protected override Task<string> InvokeCoreAsync(string input) =>
		Task.FromResult(input);
}
```

Reference the compiled DLL in `solution.tools`.

---

## LLM Clients

### Ollama

```csharp
var client = new OllamaClient(model: "llama3", endpoint: "http://localhost:11434");
```

### OpenAI

```csharp
var client = new OpenAIClient(model: "gpt-4o", apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
```

Both implement `ILLMClient`. Custom clients can be registered through `ILLMClientResolver`.

---

## Replay

When `replay.enabled` is `true`, execution records are written to `replay.storagePath`. You can replay a run deterministically:

| Mode | Behavior |
|---|---|
| `None` | Normal live execution |
| `Full` | All tool calls return recorded results |
| `ToolsOnly` | Tool calls use recorded results; other operations execute live |

---

## License

See [LICENSE](LICENSE) for details.
