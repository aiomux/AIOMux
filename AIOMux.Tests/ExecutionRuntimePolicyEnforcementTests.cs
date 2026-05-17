using AIOMux.Core;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests;

/// <summary>
/// Verifies that policy is mandatory for runtime execution and that all construction paths supply it.
/// </summary>
public sealed class ExecutionRuntimePolicyEnforcementTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoPolicyEngine_ThrowsInvalidOperationException()
    {
        var services = new ExecutionRuntimeServices
        {
            PolicyEngine = null
        };

        var plan = new ExecutionPlan
        {
            Name = "no-policy",
            Steps = [new ExecutionStep { Id = "s1", Type = "tool", Target = "noop" }]
        };

        var context = new ExecutionContext { Services = services };
        var runtime = new ExecutionRuntime();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.ExecuteAsync(plan, context));

        Assert.Contains("requires a configured policy engine", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullServices_CreatesServicesWithNoPolicyAndThrows()
    {
        var plan = new ExecutionPlan
        {
            Name = "null-services",
            Steps = [new ExecutionStep { Id = "s1", Type = "tool", Target = "noop" }]
        };

        var context = new ExecutionContext { Services = null };
        var runtime = new ExecutionRuntime();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => runtime.ExecuteAsync(plan, context));

        Assert.Contains("requires a configured policy engine", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithPolicyEngineSet_DoesNotThrowForPolicy()
    {
        var services = new ExecutionRuntimeServices
        {
            PolicyEngine = new AllowAllPolicyEngine(),
            Options = new ExecutionOptions
            {
                CollectMetrics = false,
                GenerateJobSummary = false
            }
        };

        var plan = new ExecutionPlan
        {
            Name = "with-policy",
            Steps = [new ExecutionStep { Id = "s1", Type = "tool", Target = "missing" }]
        };

        var context = new ExecutionContext { Services = services };
        var runtime = new ExecutionRuntime();

        // Should not throw due to missing policy; may fail for other reasons (missing tool).
        var result = await runtime.ExecuteAsync(plan, context);
        Assert.False(result.Success);
        Assert.DoesNotContain("requires a configured policy engine", result.Error ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForkReplayExecutor_WithNullServices_ThrowsInvalidOperationException()
    {
        var runtime = new ExecutionRuntime();
        var executor = new ForkReplayExecutor(runtime);

        var plan = new ExecutionPlan
        {
            Name = "fork",
            Steps = [new ExecutionStep { Id = "s1", Type = "tool", Target = "noop" }]
        };

        var context = new ExecutionContext { Services = null };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteForkAsync("run-1", 0, plan, context));

        Assert.Contains("requires a configured policy engine", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForkReplayExecutor_WithServicesButNoPolicyEngine_ThrowsInvalidOperationException()
    {
        var runtime = new ExecutionRuntime();
        var executor = new ForkReplayExecutor(runtime);

        var plan = new ExecutionPlan
        {
            Name = "fork-no-policy",
            Steps = [new ExecutionStep { Id = "s1", Type = "tool", Target = "noop" }]
        };

        var context = new ExecutionContext
        {
            Services = new ExecutionRuntimeServices { PolicyEngine = null }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => executor.ExecuteForkAsync("run-no-records", 0, plan, context));

        Assert.Contains("requires a configured policy engine", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
