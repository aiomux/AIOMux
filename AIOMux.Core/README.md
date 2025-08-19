# AIOMux.Core

AIOMux.Core is a modern, extensible .NET library for building, orchestrating, and scaling intelligent agent systems. It provides a robust foundation for AI-driven applications, enabling you to compose, chain, and manage agents, integrate LLMs, and extend functionality with plugins and tools—all with a clean, modular architecture.

## Key Features
- **Agent orchestration:** Register, compose, and execute agents and agent chains
- **LLM abstraction:** Plug in local or cloud LLMs (see AIOMux.LLM)
- **Plugin system:** Dynamically load agent plugins at runtime
- **Tooling:** Add custom tools for agent use
- **Configuration & validation:** Strongly-typed, extensible config
- **Metrics & memory:** Built-in support for agent metrics and memory stores

## Examples

### 1. Basic: Register and Run a Simple Agentusing AIOMux.Core;
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

### 2. Implementing a Tool (ITool)using AIOMux.Core.Interfaces;
public class UppercaseTool : ITool
{
    public string Name => "Uppercase";
    public Task<string> ExecuteAsync(string input)
        => Task.FromResult(input.ToUpperInvariant());
}

### 3. Implementing an Agent Plugin (IAgentPlugin)using AIOMux.Core.Interfaces;
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

### 4. Intermediate: Using OllamaClient from AIOMux.LLMusing AIOMux.LLM;
var llm = new OllamaClient(model: "llama3");
string response = await llm.GenerateAsync("What is the capital of France?");
Console.WriteLine(response);

### 5. Advanced: Create and Run an Agent Chain
// Assume you have two agents: agentA and agentB
manager.Register(agentA);
manager.Register(agentB);
var chain = manager.CreateChain("MyChain");
chain.AddAgent(agentA).AddAgent(agentB);
var context = new AgentContext { UserInput = "Start chain" };
var result = await chain.ExecuteAsync(context);
Console.WriteLine(result);

### 6. Advanced: Load Agent Plugins Dynamically
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
See also: [AIOMux.LLM](../AIOMux.LLM/) for LLM client implementations.

More examples soon...

## License
This project is licensed under the MIT License. See [LICENSE](../LICENSE) for details.
