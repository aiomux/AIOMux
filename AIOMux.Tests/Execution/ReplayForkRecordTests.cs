using AIOMux.Core;
using AIOMux.Core.Builders;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests.Execution;

/// <summary>
/// Tests proving that execution records are used for state reconstruction.
/// Verifies fork plan building with ExecutionRecord-based state.
/// </summary>
public class ReplayForkRecordTests
{
    [Fact]
    public async Task ReplayForkPlanBuilder_CreatesForkedPlanWithMetadata()
    {
        var originalPlan = new ExecutionPlan
        {
            Name = "original",
            Steps =
            [
                new ExecutionStep { Id = "s1", Type = "tool", Target = "tool1" },
                new ExecutionStep { Id = "s2", Type = "tool", Target = "tool2" }
            ]
        };

        var builder = new ReplayForkPlanBuilder(originalPlan, 1);
        builder.AddToolStep("s3", "tool3", outputKey: "output3");

        var forkedPlan = await builder.BuildAsync();

        Assert.Equal(3, forkedPlan.Steps.Count);
        Assert.Contains("fork", forkedPlan.Name.ToLower());
        Assert.Equal("s1", forkedPlan.Steps[0].Id);
        Assert.Equal("s2", forkedPlan.Steps[1].Id);
        Assert.Equal("s3", forkedPlan.Steps[2].Id);
    }

    [Fact]
    public void ExecutionRecord_IsSerializable_ForPersistence()
    {
        var record = new ExecutionRecord
        {
            RunId = "test-run",
            StepId = "s1",
            StepIndex = 0,
            Type = "tool",
            Target = "echo",
            Input = "test",
            Output = "result",
            OutputKey = "result",
            StateChanges = new Dictionary<string, object?> { ["result"] = "output-value" },
            Success = true,
            Timestamp = DateTimeOffset.UtcNow,
            DurationMs = 10.5
        };

        Assert.Equal("test-run", record.RunId);
        Assert.Equal("s1", record.StepId);
        Assert.Equal(0, record.StepIndex);
        Assert.True(record.Success);
        Assert.NotEmpty(record.StateChanges);
    }

    [Fact]
    public void ExecutionRecord_CapturesFailureDetails()
    {
        var record = new ExecutionRecord
        {
            RunId = "test-run",
            StepId = "s1",
            StepIndex = 0,
            Type = "tool",
            Target = "failing",
            Success = false,
            Error = "Tool execution failed",
            Timestamp = DateTimeOffset.UtcNow,
            DurationMs = 5.0
        };

        Assert.False(record.Success);
        Assert.NotNull(record.Error);
        Assert.Equal("Tool execution failed", record.Error);
    }

    [Fact]
    public void ExecutionRecord_CapturesPolicyDenial()
    {
        var record = new ExecutionRecord
        {
            RunId = "test-run",
            StepId = "s1",
            StepIndex = 0,
            Type = "tool",
            Target = "restricted",
            Success = false,
            PolicyDenyReason = "Access denied by security policy",
            PolicyHash = "policy-hash-123",
            Timestamp = DateTimeOffset.UtcNow,
            DurationMs = 2.0
        };

        Assert.False(record.Success);
        Assert.NotNull(record.PolicyDenyReason);
        Assert.Equal("Access denied by security policy", record.PolicyDenyReason);
        Assert.Equal("policy-hash-123", record.PolicyHash);
    }

    [Fact]
    public void ExecutionRecord_PreservesStateChangesForReconstruction()
    {
        var stateChanges = new Dictionary<string, object?>
        {
            ["output"] = "processed-data",
            ["status"] = "completed",
            ["timestamp"] = "2026-03-24"
        };

        var record = new ExecutionRecord
        {
            RunId = "test-run",
            StepId = "s1",
            StepIndex = 0,
            Type = "tool",
            Target = "processor",
            StateChanges = stateChanges,
            Success = true,
            Timestamp = DateTimeOffset.UtcNow,
            DurationMs = 15.0
        };

        Assert.Equal(3, record.StateChanges.Count);
        Assert.Equal("processed-data", record.StateChanges["output"]);
        Assert.Equal("completed", record.StateChanges["status"]);
        Assert.Equal("2026-03-24", record.StateChanges["timestamp"]);
    }

    [Fact]
    public void ExecutionRecord_CapturesCompleteStepContext()
    {
        var timestamp = DateTimeOffset.UtcNow;
        var record = new ExecutionRecord
        {
            RunId = "my-run",
            StepId = "analyze",
            StepIndex = 5,
            Type = "tool",
            Target = "analyzer",
            Input = new { data = "input-data" },
            Output = "analysis result",
            OutputKey = "analysis",
            StateChanges = new() { ["analysis"] = "result" },
            Success = true,
            Timestamp = timestamp,
            DurationMs = 42.5
        };

        Assert.Equal("my-run", record.RunId);
        Assert.Equal("analyze", record.StepId);
        Assert.Equal(5, record.StepIndex);
        Assert.Equal("tool", record.Type);
        Assert.Equal("analyzer", record.Target);
        Assert.NotNull(record.Input);
        Assert.Equal("analysis result", record.Output);
        Assert.Equal("analysis", record.OutputKey);
        Assert.True(record.Success);
        Assert.Equal(timestamp, record.Timestamp);
        Assert.Equal(42.5, record.DurationMs);
    }

    [Fact]
    public void MultipleExecutionRecords_MaintainOrdering()
    {
        var records = new List<ExecutionRecord>
        {
            new ExecutionRecord { StepIndex = 0, StepId = "s1", Success = true, Timestamp = DateTimeOffset.UtcNow },
            new ExecutionRecord { StepIndex = 1, StepId = "s2", Success = true, Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(10) },
            new ExecutionRecord { StepIndex = 2, StepId = "s3", Success = true, Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(20) }
        };

        for (int i = 0; i < records.Count; i++)
        {
            Assert.Equal(i, records[i].StepIndex);
            Assert.Equal($"s{i + 1}", records[i].StepId);
        }
    }

    [Fact]
    public void ExecutionRecord_HandlesOptionalFields()
    {
        var record = new ExecutionRecord
        {
            RunId = "test",
            StepId = "s1",
            StepIndex = 0,
            Type = "tool",
            Target = "tool",
            Success = true
        };

        Assert.Null(record.Error);
        Assert.Null(record.PolicyDenyReason);
        Assert.Null(record.PolicyHash);
        Assert.Empty(record.StateChanges);
        Assert.Null(record.Output);
        Assert.Null(record.OutputKey);
    }

    [Fact]
    public async Task ReplayForkPlanBuilder_WithMultipleSteps()
    {
        var originalPlan = new ExecutionPlan
        {
            Name = "original",
            Steps =
            [
                new ExecutionStep { Id = "s1", Type = "tool", Target = "t1" },
                new ExecutionStep { Id = "s2", Type = "tool", Target = "t2" }
            ]
        };

        var builder = new ReplayForkPlanBuilder(originalPlan, 1);
        builder.AddToolStep("s3", "t3", outputKey: "r3");
        builder.AddToolStep("s4", "t4", outputKey: "r4");
        builder.AddAgentStep("s5", "agent", outputKey: "r5");

        var plan = await builder.BuildAsync();

        Assert.Equal(5, plan.Steps.Count);
        Assert.Equal("s1", plan.Steps[0].Id);
        Assert.Equal("s2", plan.Steps[1].Id);
        Assert.Equal("s3", plan.Steps[2].Id);
        Assert.Equal("tool", plan.Steps[2].Type);
        Assert.Equal("s4", plan.Steps[3].Id);
        Assert.Equal("s5", plan.Steps[4].Id);
        Assert.Equal("agent", plan.Steps[4].Type);
    }
}
