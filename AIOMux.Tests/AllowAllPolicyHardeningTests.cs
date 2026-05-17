using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Local;
using Microsoft.Extensions.Logging;

namespace AIOMux.Tests;

/// <summary>
/// Tests for AllowAll policy hardening: warnings, env var rejection,
/// production mode rejection, and execution record policy type stamping.
/// </summary>
public sealed class AllowAllPolicyHardeningTests : IDisposable
{
    private const string EnvVar = "AIOMUX_DISABLE_ALLOWALL";
    private readonly string? _originalEnvValue;

    public AllowAllPolicyHardeningTests()
    {
        _originalEnvValue = Environment.GetEnvironmentVariable(EnvVar);
        Environment.SetEnvironmentVariable(EnvVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(EnvVar, _originalEnvValue);
    }

    // -------------------------------------------------------------------------
    // AllowAllPolicyEngine unit tests
    // -------------------------------------------------------------------------

    [Fact]
    public void AllowAllPolicyEngine_EmitsThreeWarnings_OnConstruction()
    {
        var logger = new CapturingLogger();
        _ = new AllowAllPolicyEngine(logger);

        Assert.Contains(logger.Messages, m => m.Contains("AllowAllPolicyEngine is enabled"));
        Assert.Contains(logger.Messages, m => m.Contains("All tool operations will be permitted"));
        Assert.Contains(logger.Messages, m => m.Contains("unsafe for production use"));
    }

    [Fact]
    public void AllowAllPolicyEngine_ReportsPolicyType_AsAllowAll()
    {
        IPolicyEngine engine = new AllowAllPolicyEngine();
        Assert.Equal("allowall", engine.PolicyType);
    }

    [Fact]
    public void AllowAllPolicyEngine_AllowsAllCalls()
    {
        var engine = new AllowAllPolicyEngine();
        var decision = engine.Evaluate(
            new ToolCall { ToolName = "any-tool", Input = "input" },
            ToolExecutionAnalysis.Recognized(ToolOperation.Read),
            new AgentContext { RunId = "test-run" });

        Assert.True(decision.Allowed);
    }

    // -------------------------------------------------------------------------
    // SolutionRunner env var guard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ThrowsOnAllowAll_WhenDisableEnvVarIsTrue()
    {
        Environment.SetEnvironmentVariable(EnvVar, "true");

        var dir = CreateTempSolution(mode: null);
        try
        {
            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => runner.LoadAsync(Path.Combine(dir, "solution.json")));

            Assert.Contains("AIOMUX_DISABLE_ALLOWALL", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_SucceedsOnAllowAll_WhenDisableEnvVarIsAbsent()
    {
        var dir = CreateTempSolution(mode: null);
        try
        {
            var runner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["echo"] = new EchoTool()
                });

            // Should not throw.
            var loaded = await runner.LoadAsync(Path.Combine(dir, "solution.json"));
            Assert.NotNull(loaded);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // SolutionRunner production mode guard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task LoadAsync_ThrowsOnAllowAll_WhenModeIsProduction()
    {
        var dir = CreateTempSolution(mode: "Production");
        try
        {
            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => runner.LoadAsync(Path.Combine(dir, "solution.json")));

            Assert.Contains("Production", ex.Message);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_SucceedsOnAllowAll_WhenModeIsDevelopment()
    {
        var dir = CreateTempSolution(mode: "Development");
        try
        {
            var runner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["echo"] = new EchoTool()
                });

            var loaded = await runner.LoadAsync(Path.Combine(dir, "solution.json"));
            Assert.NotNull(loaded);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // PolicyType recorded in ExecutionRecord
    // -------------------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithAllowAllPolicy_StampsPolicyTypeOnExecutionRecord()
    {
        var dir = CreateTempSolution(mode: null);
        try
        {
            var runner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["echo"] = new EchoTool()
                });

            var summary = await runner.RunAsync(Path.Combine(dir, "solution.json"), "hello");

            Assert.True(summary.Success, summary.Error);
            Assert.NotEmpty(summary.Records);
            Assert.All(summary.Records, r => Assert.Equal("allowall", r.PolicyType));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string CreateTempSolution(string? mode)
    {
        var dir = Path.Combine(Path.GetTempPath(), "aiomux-allowall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "plan.json"),
            """
            {
              "name": "allowall-plan",
              "steps": [
                {
                  "id": "step1",
                  "type": "tool",
                  "target": "echo",
                  "bindings": { "input": "inputs.input" }
                }
              ]
            }
            """);

        File.WriteAllText(Path.Combine(dir, "policy.json"),
            """
            {
              "type": "allowall"
            }
            """);

        var modeJson = mode is null ? "" : $"""  "mode": "{mode}",""" + "\n";

        File.WriteAllText(Path.Combine(dir, "solution.json"),
            $$"""
            {
              "name": "allowall-solution",
              {{modeJson}}  "entry": "plan.json",
              "policyConfig": "policy.json",
              "agents": [],
              "tools": [],
              "connectors": [],
              "executionOptions": {
                "collectMetrics": false,
                "generateJobSummary": false,
                "includeDetailedMetrics": false
              }
            }
            """);

        return dir;
    }

    private sealed class EchoTool : DispatchableToolBase
    {
        public override string Name => "echo";
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input) => Task.FromResult(input);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
