using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Local;

namespace AIOMux.Tests;

public sealed class SolutionRunnerIntegrationTests
{
    [Fact]
    public async Task RunAsync_WithToolDenyListPolicy_DeniesToolAndRecordsPolicyReason()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-policy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "policy-plan",
                  "steps": [
                    {
                      "id": "deny-step",
                      "type": "tool",
                      "target": "exfiltrate",
                      "bindings": {
                        "input": "inputs.input"
                      }
                    }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "tooldenylist",
                  "parameters": {
                    "denyTools": ["exfiltrate"]
                  }
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "policy-solution",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "assemblies": [],
                  "executionOptions": {
                    "collectMetrics": false,
                    "generateJobSummary": false,
                    "includeDetailedMetrics": false
                  }
                }
                """);

            var tool = new TrackingExfiltrateTool();
            var runner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["exfiltrate"] = tool
                });

            var summary = await runner.RunAsync(Path.Combine(solutionDirectory, "solution.json"), "secret");

            Assert.False(summary.Success);
            Assert.Equal("Policy denied tool: exfiltrate", summary.Error);
            Assert.False(tool.WasCalled);
            Assert.Single(summary.Records);
            Assert.Equal("Policy denied tool: exfiltrate", summary.Records[0].PolicyDenyReason);
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithInjectedTool_ExecutesToolStep()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-tools-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "tool-plan",
                  "steps": [
                    {
                      "id": "reverse-step",
                      "type": "tool",
                      "target": "reverse",
                      "bindings": {
                        "input": "inputs.input"
                      }
                    }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "tool-solution",
                  "entry": "plan.json",
                  "assemblies": [],
                  "executionOptions": {
                    "collectMetrics": false,
                    "generateJobSummary": false,
                    "includeDetailedMetrics": false
                  }
                }
                """);

            var runner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["reverse"] = new ReverseTool()
                });

            var summary = await runner.RunAsync(Path.Combine(solutionDirectory, "solution.json"), "abcd");

            Assert.True(summary.Success, summary.Error);
            Assert.Equal("dcba", summary.Output);
            Assert.Equal(1, summary.ExecutedSteps);
            Assert.Null(summary.Error);
            Assert.False(string.IsNullOrWhiteSpace(summary.RunId));

            var localRunPath = Path.Combine(solutionDirectory, "runs", $"{summary.RunId}.records.json");
            Assert.True(File.Exists(localRunPath), $"Expected run record file at '{localRunPath}'");
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ReplayAsync_ReusesRecordedToolResults_WhenToolsWouldThrow()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);

        try
        {
            var planPath = Path.Combine(solutionDirectory, "plan.json");
            await File.WriteAllTextAsync(planPath,
                """
                {
                  "name": "replay-plan",
                  "steps": [
                    {
                      "id": "first",
                      "type": "tool",
                      "target": "first",
                      "bindings": {
                        "input": "inputs.input"
                      },
                      "outputKey": "firstOut"
                    },
                    {
                      "id": "second",
                      "type": "tool",
                      "target": "second",
                      "bindings": {
                        "input": "state.firstOut"
                      }
                    }
                  ]
                }
                """);

            var solutionPath = Path.Combine(solutionDirectory, "solution.json");
            await File.WriteAllTextAsync(solutionPath,
                """
                {
                  "name": "replay-solution",
                  "entry": "plan.json",
                  "assemblies": [],
                  "executionOptions": {
                    "collectMetrics": false,
                    "generateJobSummary": false,
                    "includeDetailedMetrics": false
                  }
                }
                """);

            var sourceRunner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["first"] = new PrefixTool("S:"),
                    ["second"] = new PrefixTool("T:")
                });

            var sourceSummary = await sourceRunner.RunAsync(solutionPath, "payload");
            Assert.True(sourceSummary.Success, sourceSummary.Error);
            Assert.False(string.IsNullOrWhiteSpace(sourceSummary.RunId));

            var replayRunner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["first"] = new ThrowIfCalledTool(),
                    ["second"] = new ThrowIfCalledTool()
                });

            var replaySummary = await replayRunner.ReplayAsync(solutionPath, sourceSummary.RunId!);

            Assert.True(replaySummary.Success, replaySummary.Error);
            Assert.Equal("T:S:payload", replaySummary.Output);
            Assert.Equal(2, replaySummary.ExecutedSteps);
            Assert.Equal(PlanSource.Static, replaySummary.PlanSource);
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ForkAsync_AtStepZero_ReplaysFirstToolAndExecutesFollowingSteps()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-fork-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);

        try
        {
            var planPath = Path.Combine(solutionDirectory, "plan.json");
            await File.WriteAllTextAsync(planPath,
                """
                {
                  "name": "fork-plan",
                  "steps": [
                    {
                      "id": "first",
                      "type": "tool",
                      "target": "first",
                      "bindings": {
                        "input": "inputs.input"
                      },
                      "outputKey": "firstOut"
                    },
                    {
                      "id": "second",
                      "type": "tool",
                      "target": "second",
                      "bindings": {
                        "input": "state.firstOut"
                      }
                    }
                  ]
                }
                """);

            var solutionPath = Path.Combine(solutionDirectory, "solution.json");
            await File.WriteAllTextAsync(solutionPath,
                """
                {
                  "name": "fork-solution",
                  "entry": "plan.json",
                  "assemblies": [],
                  "executionOptions": {
                    "collectMetrics": false,
                    "generateJobSummary": false,
                    "includeDetailedMetrics": false
                  }
                }
                """);

            var sourceRunner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["first"] = new PrefixTool("S:"),
                    ["second"] = new PrefixTool("T:")
                });

            var sourceSummary = await sourceRunner.RunAsync(solutionPath, "payload");
            Assert.True(sourceSummary.Success, sourceSummary.Error);
            Assert.False(string.IsNullOrWhiteSpace(sourceSummary.RunId));

            var firstTool = new ThrowIfCalledTool();
            var forkRunner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["first"] = firstTool,
                    ["second"] = new PrefixTool("Fork:")
                });

            var forkSummary = await forkRunner.ForkAsync(solutionPath, sourceSummary.RunId!, forkStepIndex: 0);

            Assert.True(forkSummary.Success, forkSummary.Error);
            Assert.Equal("Fork:S:payload", forkSummary.Output);
            Assert.False(firstTool.WasCalled);
            Assert.Equal(PlanSource.ReplayFork, forkSummary.PlanSource);
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    private sealed class TrackingExfiltrateTool : ITool
    {
        public string Name => "exfiltrate";

        public bool WasCalled { get; private set; }

        public Task<string> ExecuteAsync(string input)
        {
            WasCalled = true;
            return Task.FromResult($"EXFILTRATED: {input}");
        }
    }

    private sealed class ReverseTool : ITool
    {
        public string Name => "reverse";

        public Task<string> ExecuteAsync(string input)
            => Task.FromResult(new string(input.Reverse().ToArray()));
    }

    private sealed class PrefixTool : ITool
    {
        private readonly string _prefix;

        public PrefixTool(string prefix)
        {
            _prefix = prefix;
        }

        public string Name => "prefix";

        public Task<string> ExecuteAsync(string input)
            => Task.FromResult(_prefix + input);
    }

    private sealed class ThrowIfCalledTool : ITool
    {
        public string Name => "throw";

        public bool WasCalled { get; private set; }

        public Task<string> ExecuteAsync(string input)
        {
            WasCalled = true;
            throw new InvalidOperationException("Tool should not be called while replaying.");
        }
    }
}
