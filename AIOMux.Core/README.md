# AIOMux.Core

AIOMux.Core is a modern, extensible .NET library for building, orchestrating, and scaling intelligent agent systems. It provides a robust foundation for AI-driven applications, enabling you to compose, chain, and manage agents, integrate LLMs, and extend functionality with plugins and tools-all with a clean, modular architecture.

## Key Features
- **Agent orchestration:** Register, compose, and execute agents and agent chains
- **LLM abstraction:** Plug in local or cloud LLMs (see AIOMux.LLM)
- **Plugin system:** Dynamically load agent plugins at runtime
- **Tooling:** Add custom tools for agent use
- **Configuration & validation:** Strongly-typed, extensible config
- **Metrics & memory:** Built-in support for agent metrics and memory stores
- **Host-friendly facade:** `IAgentRuntime` for decoupled execution from web/Discord/local runners
- **Cancellation support:** Optional `ICancellableAgent` interface for long-running operations

## Examples

### 1. Basic: Register and Run a Simple Agent

```
using AIOMux.Core;
using AIOMux.Core.Interfaces;

public class EchoAgent : IAgent
{
    public string Name => "EchoAgent";
    public Task<string> ExecuteAsync(AgentContext context)
        => Task.FromResult($"Echo: {context.UserInput}");
}

var manager = new AgentManager();
manager.Register(new EchoAgent());
var agent = manager.GetByName("EchoAgent");
if (agent != null)
{
    var context = new AgentContext { UserInput = "Hello!" };
    var result = await agent.ExecuteAsync(context);
    Console.WriteLine(result);
}
```

### 2. Using the IAgentRuntime Facade (Recommended for Hosts)

The `IAgentRuntime` facade provides a stable, simple API for hosts (web, Discord, local runners) without exposing internal orchestration details.

```csharp
using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

// Setup
var manager = new AgentManager();
manager.Register(new EchoAgent());
var orchestrator = new AgentOrchestrator(manager);
var runtime = new AgentRuntime(manager, orchestrator);

// Execute a single agent
var agentRequest = new AgentRunRequest
{
    AgentName = "EchoAgent",
    Context = new AgentContext { UserInput = "Hello!" }
};

var result = await runtime.RunAsync(agentRequest);

if (result.Success)
{
    Console.WriteLine($"Success: {result.Output}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}

// Execute with cancellation support
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var result = await runtime.RunAsync(agentRequest, cts.Token);
```

### 3. Supporting Cancellation in Your Agents

To support cancellation tokens, implement `ICancellableAgent`:

```csharp
using AIOMux.Core.Interfaces;

public class LongRunningAgent : ICancellableAgent
{
    public string Name => "LongRunner";

    public async Task<string> ExecuteAsync(AgentContext context)
    {
        // Default implementation without cancellation
        return await ExecuteAsync(context, CancellationToken.None);
    }

    public async Task<string> ExecuteAsync(AgentContext context, CancellationToken cancellationToken)
    {
        // Respect the cancellation token
        for (int i = 0; i < 10; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(1000, cancellationToken);
        }
        return "Completed!";
    }
}
```

### 4. Implementing a Tool (ITool)
```
using AIOMux.Core.Interfaces;
public class UppercaseTool : ITool
{
    public string Name => "Uppercase";
    public Task<string> ExecuteAsync(string input)
        => Task.FromResult(input.ToUpperInvariant());
}
```
### 5. Implementing an Agent Plugin (IAgentPlugin)
```
using AIOMux.Core.Interfaces;
public class MyPlugin : IAgentPlugin
{
    public AgentMetadata Metadata => new() { Name = "MyPluginAgent", Description = "A sample plugin agent." };
    public IAgent CreateAgent(ILLMClient? llmClient = null, Dictionary<string, object>? configuration = null)
        => new MyPluginAgent();
    public Task<bool> InitializeAsync(Dictionary<string, object>? configuration = null) => Task.FromResult(true);
    public Task DisposeAsync() => Task.CompletedTask;
}

public class MyPluginAgent : IAgent
{
    public string Name => "MyPluginAgent";
    public Task<string> ExecuteAsync(AgentContext context)
        => Task.FromResult("Plugin agent executed!");
}
```
### 6. Intermediate: Using OllamaClient from AIOMux.Clients
```
using AIOMux.Clients;
var llm = new OllamaClient(model: "llama3");
string response = await llm.GenerateAsync("What is the capital of France?");
Console.WriteLine(response);
```

### 7. Advanced: Create and Run an Agent Chain
```
// Assume you have two agents: agentA and agentB
manager.Register(agentA);
manager.Register(agentB);
var chain = manager.CreateChain("MyChain");
chain.AddAgent(agentA).AddAgent(agentB);
var context = new AgentContext { UserInput = "Start chain" };
var result = await chain.ExecuteAsync(context);
Console.WriteLine(result);
```
### 8. Advanced: Load Agent Plugins Dynamically
```
var manager = new AgentManager();
bool loaded = await manager.LoadPluginAsync("./plugins/AIOMux.Plugin.MyPlugin.dll");
if (loaded)
{
    var pluginAgent = manager.GetByName("MyPluginAgent");
    if (pluginAgent != null)
    {
        var context = new AgentContext { UserInput = "Run plugin agent" };
        var result = await pluginAgent.ExecuteAsync(context);
        Console.WriteLine(result);
    }
}
```

## New APIs (Host-Friendly Facade)

### IAgentRuntime

High-level interface for executing agents or chains without exposing internal details:

```csharp
public interface IAgentRuntime
{
    Task<AgentRuntimeResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default);
}
```

### AgentRunRequest

```csharp
public sealed record AgentRunRequest
{
    public string? AgentName { get; init; }      // Either this
    public string? ChainName { get; init; }      // Or this (mutually exclusive)
    public AgentContext Context { get; init; }   // Required
}
```

### AgentRuntimeResult

```csharp
public sealed record AgentRuntimeResult
{
    public bool Success { get; init; }           // Execution succeeded
    public string Output { get; init; }          // Agent output
    public string? Error { get; init; }          // Error message if failed
    public int? StepIndex { get; init; }         // Chain failure step index
    public string? AgentName { get; init; }      // Agent/chain name
}
```

### ICancellableAgent

Optional interface for agents that support cancellation tokens:

```csharp
public interface ICancellableAgent : IAgent
{
    Task<string> ExecuteAsync(AgentContext context, CancellationToken cancellationToken);
}
```

## Design Notes

- **Original user input preservation:** The orchestrator preserves `context.UserInput` in `context.Variables["user.input.original"]` for replay-friendly execution.
- **Case-insensitive contexts:** `Variables` and `Tools` dictionaries use `StringComparer.OrdinalIgnoreCase` for consistent lookups.
- **No-op memory store:** By default, `AgentContext.Memory` uses `NullMemoryStore` (no-op) to reduce allocations. Hosts can override if needed.
- **Backward compatible:** All existing `IAgent` implementations remain unchanged; cancellation support is opt-in via `ICancellableAgent`.
