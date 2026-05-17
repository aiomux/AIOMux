using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using System.Collections.Immutable;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests;

public sealed class AgentManagerTests
{
    [Fact]
    public void GetFormattedAgentList_ReturnsAllRegisteredAgents()
    {
        var manager = new AgentManager();
        manager.Register(new EchoAgent());
        manager.Register(new UtilityAgent());

        var output = manager.GetFormattedAgentList();

        Assert.Contains("echo", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("utility", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAvailableAgents_ReturnsAllRegisteredAgents()
    {
        var manager = new AgentManager();
        manager.Register(new EchoAgent());
        manager.Register(new UtilityAgent());

        var available = manager.GetAvailableAgents().ToList();

        Assert.Equal(2, available.Count);
        Assert.Contains(available, a => a.Name.Equals("echo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(available, a => a.Name.Equals("utility", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LoadAgentsFromAssemblyAsync_WhenAssemblyMissing_ReturnsFalse()
    {
        var manager = new AgentManager();

        var loaded = await manager.LoadAgentsFromAssemblyAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll"));

        Assert.False(loaded);
    }

    private sealed class UtilityAgent : IAgent
    {
        public string Name => "utility";

        public string Description => "A generic utility agent for testing.";

        public AgentMetadata Metadata => new();

        public Task<StepExecutionResult> ExecuteAsync(
            ImmutableDictionary<string, object?> inputs,
            ExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new StepExecutionResult { Success = true, Output = "ok" });
    }
}
