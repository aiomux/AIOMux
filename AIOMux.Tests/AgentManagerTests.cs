using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using System.Collections.Immutable;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests;

public sealed class AgentManagerTests
{
    [Fact]
    public void GetFormattedAgentList_ExcludesPlannerAgents()
    {
        var manager = new AgentManager();
        manager.Register(new EchoAgent());
        manager.Register(new PlannerNamedAgent());

        var output = manager.GetFormattedAgentList();

        Assert.Contains("echo", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("planner", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetAvailableAgents_ExcludesPlannerAgents()
    {
        var manager = new AgentManager();
        manager.Register(new EchoAgent());
        manager.Register(new PlannerNamedAgent());

        var available = manager.GetAvailableAgents().ToList();

        Assert.Single(available);
        Assert.Equal("echo", available[0].Name);
    }

    [Fact]
    public async Task LoadAgentsFromAssemblyAsync_WhenAssemblyMissing_ReturnsFalse()
    {
        var manager = new AgentManager();

        var loaded = await manager.LoadAgentsFromAssemblyAsync(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dll"));

        Assert.False(loaded);
    }

    private sealed class PlannerNamedAgent : IAgent
    {
        public string Name => "PlannerAnything";

        public string Description => "planner-like";

        public Task<StepExecutionResult> ExecuteAsync(
            ImmutableDictionary<string, object?> inputs,
            ExecutionContext context,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new StepExecutionResult { Success = true, Output = "ok" });
    }
}
