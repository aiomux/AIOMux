using AIOMux.Core;
using AIOMux.Core.Builders;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using System.Collections.Immutable;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests.Execution;

/// <summary>
/// Tests proving that ExecutionRecord is the sole execution trace model.
/// Verifies all step outcomes (success, failure, denial) are recorded.
/// </summary>
public class ExecutionRecordingTests
{
    private ExecutionRuntimeServices CreateServices(
        Dictionary<string, ITool>? tools = null,
        IAgentManager? agentManager = null,
        IPolicyEngine? policyEngine = null)
    {
        tools ??= new();
        agentManager ??= new AgentManager();
        policyEngine ??= new AllowAllPolicyEngine();

        return new ExecutionRuntimeServices
        {
            Tools = tools,
            AgentManager = agentManager,
            PolicyEngine = policyEngine,
            Options = new ExecutionOptions { GenerateJobSummary = false, CollectMetrics = false },
            ReplayMode = ReplayMode.None,
            ReplayToolResults = new Dictionary<string, ToolResult>()
        };
    }

    [Fact]
    public async Task ExecuteAsync_SingleToolStep_CreatesOneExecutionRecord()
    {
        var runtime = new ExecutionRuntime();
        var context = new ExecutionContext { RunId = Guid.NewGuid().ToString(), Inputs = new() };
        var services = CreateServices(tools: new()
        {
            ["echo"] = new MockTool("echo", "test-output")
        });
        context.Services = services;

        var plan = new ExecutionPlan
        {
            Name = "single-step-plan",
            Steps =
            [
                new ExecutionStep { Id = "step-1", Type = "tool", Target = "echo", OutputKey = "result" }
            ]
        };

        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success);
        Assert.Single(context.Records);
        var record = context.Records[0];
        Assert.Equal("step-1", record.StepId);
        Assert.Equal(0, record.StepIndex);
        Assert.Equal("tool", record.Type);
        Assert.Equal("echo", record.Target);
        Assert.True(record.Success);
        Assert.Equal("test-output", record.Output);
        Assert.Equal("result", record.OutputKey);
        Assert.Contains("result", record.StateChanges.Keys);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleToolSteps_CreatesOrderedExecutionRecords()
    {
        var runtime = new ExecutionRuntime();
        var context = new ExecutionContext { RunId = Guid.NewGuid().ToString(), Inputs = new() };
        var services = CreateServices(tools: new()
        {
            ["step1-tool"] = new MockTool("step1-tool", "output-1"),
            ["step2-tool"] = new MockTool("step2-tool", "output-2"),
            ["step3-tool"] = new MockTool("step3-tool", "output-3")
        });
        context.Services = services;

        var plan = new ExecutionPlan
        {
            Name = "multi-step-plan",
            Steps =
            [
                new ExecutionStep { Id = "s1", Type = "tool", Target = "step1-tool", OutputKey = "r1" },
                new ExecutionStep { Id = "s2", Type = "tool", Target = "step2-tool", OutputKey = "r2" },
                new ExecutionStep { Id = "s3", Type = "tool", Target = "step3-tool", OutputKey = "r3" }
            ]
        };

        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success);
        Assert.Equal(3, context.Records.Count);

        for (int i = 0; i < 3; i++)
        {
            var record = context.Records[i];
            Assert.Equal(i, record.StepIndex);
            Assert.Equal($"s{i + 1}", record.StepId);
            Assert.True(record.Success);
        }

        Assert.Equal("output-1", context.Records[0].Output);
        Assert.Equal("output-2", context.Records[1].Output);
        Assert.Equal("output-3", context.Records[2].Output);
    }

    [Fact]
    public async Task ExecuteAsync_FailedStep_RecordsFailureWithError()
    {
        var runtime = new ExecutionRuntime();
        var context = new ExecutionContext { RunId = Guid.NewGuid().ToString(), Inputs = new() };
        var services = CreateServices(tools: new()
        {
            ["failing-tool"] = new MockTool("failing-tool", null, throwError: "Tool execution failed")
        });
        context.Services = services;

        var plan = new ExecutionPlan
        {
            Name = "failure-plan",
            Steps =
            [
                new ExecutionStep { Id = "fail-step", Type = "tool", Target = "failing-tool" }
            ]
        };

        var result = await runtime.ExecuteAsync(plan, context);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Error ?? "");
        Assert.Single(context.Records);

        var record = context.Records[0];
        Assert.Equal("fail-step", record.StepId);
        Assert.False(record.Success);
        Assert.NotNull(record.Error);
        Assert.Contains("Tool execution failed", record.Error);
    }

    [Fact]
    public async Task ExecuteAsync_DeniedByPolicy_RecordsWithPolicyDenyReason()
    {
        var runtime = new ExecutionRuntime();
        var context = new ExecutionContext { RunId = Guid.NewGuid().ToString(), Inputs = new() };

        var denyingPolicy = new DenyingPolicyEngine("Tool access denied by security policy");
        var services = CreateServices(
            tools: new() { ["restricted"] = new MockTool("restricted", "output") },
            policyEngine: denyingPolicy
        );
        context.Services = services;

        var plan = new ExecutionPlan
        {
            Name = "denied-plan",
            Steps =
            [
                new ExecutionStep { Id = "denied-step", Type = "tool", Target = "restricted" }
            ]
        };

        var result = await runtime.ExecuteAsync(plan, context);

        Assert.False(result.Success);
        Assert.Single(context.Records);

        var record = context.Records[0];
        Assert.Equal("denied-step", record.StepId);
        Assert.False(record.Success);
        Assert.NotNull(record.PolicyDenyReason);
        Assert.Equal("Tool access denied by security policy", record.PolicyDenyReason);
    }

    [Fact]
    public async Task ExecuteAsync_RecordCapturesStateChanges()
    {
        var runtime = new ExecutionRuntime();
        var context = new ExecutionContext { RunId = Guid.NewGuid().ToString(), Inputs = new() };
        var services = CreateServices(tools: new()
        {
            ["stateful"] = new MockTool("stateful", "primary-output")
        });
        context.Services = services;

        var plan = new ExecutionPlan
        {
            Name = "state-mutation-plan",
            Steps =
            [
                new ExecutionStep 
                { 
                    Id = "state-step", 
                    Type = "tool", 
                    Target = "stateful",
                    OutputKey = "primary"
                }
            ]
        };

        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success);
        var record = context.Records[0];
        Assert.NotEmpty(record.StateChanges);
        Assert.Contains("primary", record.StateChanges.Keys);
        Assert.Equal("primary-output", record.StateChanges["primary"]);
    }

    [Fact]
    public async Task ExecuteAsync_RecordIncludesTimingMetadata()
    {
        var runtime = new ExecutionRuntime();
        var context = new ExecutionContext { RunId = Guid.NewGuid().ToString(), Inputs = new() };
        var services = CreateServices(tools: new()
        {
            ["timed"] = new MockTool("timed", "output")
        });
        context.Services = services;

        var plan = new ExecutionPlan
        {
            Name = "timing-plan",
            Steps =
            [
                new ExecutionStep { Id = "timed-step", Type = "tool", Target = "timed" }
            ]
        };

        var beforeExecution = DateTimeOffset.UtcNow;
        var result = await runtime.ExecuteAsync(plan, context);
        var afterExecution = DateTimeOffset.UtcNow;

        Assert.True(result.Success);
        var record = context.Records[0];
        Assert.True(record.Timestamp >= beforeExecution);
        Assert.True(record.Timestamp <= afterExecution);
        Assert.True(record.DurationMs >= 0);
    }

    [Fact]
    public async Task ExecuteAsync_StopOnFirstFailure_DoesNotContinueAfterFailedStep()
    {
        var runtime = new ExecutionRuntime();
        var context = new ExecutionContext { RunId = Guid.NewGuid().ToString(), Inputs = new() };
        var services = CreateServices(tools: new()
        {
            ["step1"] = new MockTool("step1", "output-1"),
            ["failing"] = new MockTool("failing", null, throwError: "Step failed"),
            ["step3"] = new MockTool("step3", "output-3")
        });
        context.Services = services;

        var plan = new ExecutionPlan
        {
            Name = "partial-plan",
            Steps =
            [
                new ExecutionStep { Id = "s1", Type = "tool", Target = "step1", OutputKey = "r1" },
                new ExecutionStep { Id = "s2", Type = "tool", Target = "failing", OutputKey = "r2" },
                new ExecutionStep { Id = "s3", Type = "tool", Target = "step3", OutputKey = "r3" }
            ]
        };

        var result = await runtime.ExecuteAsync(plan, context);

        Assert.False(result.Success);
        Assert.Equal(2, context.Records.Count);
        Assert.True(context.Records[0].Success);
        Assert.False(context.Records[1].Success);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsIncludeRunIdAndStepIndexing()
    {
        var runId = "test-run-" + Guid.NewGuid();
        var runtime = new ExecutionRuntime();
        var context = new ExecutionContext { RunId = runId, Inputs = new() };
        var services = CreateServices(tools: new()
        {
            ["tool-a"] = new MockTool("tool-a", "a"),
            ["tool-b"] = new MockTool("tool-b", "b")
        });
        context.Services = services;

        var plan = new ExecutionPlan
        {
            Name = "indexed-plan",
            Steps =
            [
                new ExecutionStep { Id = "step-a", Type = "tool", Target = "tool-a" },
                new ExecutionStep { Id = "step-b", Type = "tool", Target = "tool-b" }
            ]
        };

        var result = await runtime.ExecuteAsync(plan, context);

        Assert.True(result.Success);
        Assert.Equal(2, context.Records.Count);

        for (int i = 0; i < context.Records.Count; i++)
        {
            var record = context.Records[i];
            Assert.Equal(runId, record.RunId);
            Assert.Equal(i, record.StepIndex);
        }
    }
}

/// <summary>Mock tool for testing.</summary>
internal class MockTool : ITool
{
    private readonly string? _output;
    private readonly string? _error;

    public string Name { get; }

    public MockTool(string name, string? output = null, string? throwError = null)
    {
        Name = name;
        _output = output;
        _error = throwError;
    }

    public Task<string> ExecuteAsync(string input)
    {
        if (_error != null)
            throw new InvalidOperationException(_error);
        return Task.FromResult(_output ?? input);
    }
}

/// <summary>Policy engine that denies all steps.</summary>
internal class DenyingPolicyEngine : IPolicyEngine
{
    private readonly string _denyReason;

    public DenyingPolicyEngine(string denyReason)
    {
        _denyReason = denyReason;
    }

    public PolicyDecision EvaluateStep(
        ExecutionStepMetadata stepMetadata,
        IReadOnlyDictionary<string, object?> resolvedInputs,
        ExecutionContext context)
    {
        return PolicyDecision.Deny(_denyReason, "test-policy");
    }
}
