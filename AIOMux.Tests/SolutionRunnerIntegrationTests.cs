using AIOMux.Clients;
using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Local;
using System.Reflection;

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

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "allowall"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "tool-solution",
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
    public async Task LoadAsync_WithOpenAiProfileAndEnvironmentVariable_UsesEnvironmentValue()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-openai-env-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);
        var envVarName = "AIOMUX_TEST_OPENAI_KEY_" + Guid.NewGuid().ToString("N");
        const string expectedKey = "env-test-key";

        try
        {
            Environment.SetEnvironmentVariable(envVarName, expectedKey);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "openai-plan",
                  "steps": [
                    {
                      "id": "echo-step",
                      "type": "agent",
                      "target": "echo"
                    }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "allowall"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                $$"""
                {
                  "name": "openai-solution",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "llmProfiles": {
                    "default": {
                      "provider": "openai",
                      "model": "gpt-4o-mini",
                      "apiKeyEnvironmentVariable": "{{envVarName}}",
                      "apiKey": "inline-fallback-key"
                    }
                  },
                  "assemblies": []
                }
                """
            );

            var runner = new SolutionRunner();
            var loaded = await runner.LoadAsync(Path.Combine(solutionDirectory, "solution.json"));

            var planner = Assert.IsType<PlannerAgent>(loaded.Services.AgentManager?.GetByName("PlannerAgent"));
            var clientField = typeof(PlannerAgent).GetField("_llmClient", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(clientField);

            var client = Assert.IsType<OpenAIClient>(clientField!.GetValue(planner));
            var httpField = typeof(OpenAIClient).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(httpField);

            var httpClient = Assert.IsType<HttpClient>(httpField!.GetValue(client));
            var token = httpClient.DefaultRequestHeaders.Authorization?.Parameter;
            Assert.Equal(expectedKey, token);
            Assert.NotEqual("inline-fallback-key", token);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithOpenAiProfileAndMissingKey_ThrowsClearError()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-openai-missing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);
        var envVarName = "AIOMUX_TEST_OPENAI_MISSING_" + Guid.NewGuid().ToString("N");

        try
        {
            Environment.SetEnvironmentVariable(envVarName, null);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "openai-missing-plan",
                  "steps": [
                    {
                      "id": "echo-step",
                      "type": "agent",
                      "target": "echo"
                    }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "allowall"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                $$"""
                {
                  "name": "openai-missing-solution",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "llmProfiles": {
                    "default": {
                      "provider": "openai",
                      "model": "gpt-4o-mini",
                      "apiKeyEnvironmentVariable": "{{envVarName}}"
                    }
                  },
                  "assemblies": []
                }
                """
            );

            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runner.LoadAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("OpenAI LLM profile requires an API key", ex.Message, StringComparison.Ordinal);
            Assert.Contains(envVarName, ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithMissingOllamaModel_ThrowsClearError()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-ollama-nomodel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "ollama-nomodel-plan",
                  "steps": [
                    {
                      "id": "echo-step",
                      "type": "agent",
                      "target": "echo"
                    }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "allowall"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "ollama-nomodel-solution",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "llmProfiles": {
                    "default": {
                      "provider": "ollama",
                      "endpoint": "http://localhost:11434"
                    }
                  },
                  "assemblies": []
                }
                """
            );

            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runner.LoadAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("Ollama LLM profile must specify a 'model'", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithMissingOllamaEndpoint_ThrowsClearError()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-ollama-noendpoint-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "ollama-noendpoint-plan",
                  "steps": [
                    {
                      "id": "echo-step",
                      "type": "agent",
                      "target": "echo"
                    }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "allowall"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "ollama-noendpoint-solution",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "llmProfiles": {
                    "default": {
                      "provider": "ollama",
                      "model": "llama3"
                    }
                  },
                  "assemblies": []
                }
                """
            );

            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runner.LoadAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("Ollama LLM profile must specify an 'endpoint'", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithMissingOpenAiModel_ThrowsClearError()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-openai-nomodel-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);
        var envVarName = "AIOMUX_TEST_OPENAI_NOMODEL_" + Guid.NewGuid().ToString("N");

        try
        {
            Environment.SetEnvironmentVariable(envVarName, "test-key");

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "openai-nomodel-plan",
                  "steps": [
                    {
                      "id": "echo-step",
                      "type": "agent",
                      "target": "echo"
                    }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "allowall"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                $$"""
                {
                  "name": "openai-nomodel-solution",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "llmProfiles": {
                    "default": {
                      "provider": "openai",
                      "apiKeyEnvironmentVariable": "{{envVarName}}"
                    }
                  },
                  "assemblies": []
                }
                """
            );

            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runner.LoadAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("OpenAI LLM profile must specify a 'model'", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(envVarName, null);
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAsync_WithNegativeMaxRequestsPerMinute_ThrowsClearError()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-negative-rpm-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "negative-rpm-plan",
                  "steps": [
                    {
                      "id": "echo-step",
                      "type": "agent",
                      "target": "echo"
                    }
                  ]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "allowall"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "negative-rpm-solution",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "llmProfiles": {
                    "default": {
                      "provider": "ollama",
                      "model": "llama3",
                      "endpoint": "http://localhost:11434",
                      "maxRequestsPerMinute": -1
                    }
                  },
                  "assemblies": []
                }
                """
            );

            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                runner.LoadAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("maxRequestsPerMinute' must be null or greater than zero", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_WithOpenAiApiKeyInConfiguration_DoesNotIncludeApiKeyInExecutionRecords()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-openai-redact-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);
        const string sensitiveKey = "test-sensitive-openai-key";

        try
        {
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
                """
                {
                  "name": "openai-redact-plan",
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

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
                """
                {
                  "type": "allowall"
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                $$"""
                {
                  "name": "openai-redact-solution",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "llmProfiles": {
                    "default": {
                      "provider": "openai",
                      "model": "gpt-4o-mini",
                      "apiKey": "{{sensitiveKey}}"
                    }
                  },
                  "assemblies": []
                }
                """
            );

            var runner = new SolutionRunner(
                tools: new Dictionary<string, ITool>(StringComparer.OrdinalIgnoreCase)
                {
                    ["reverse"] = new DispatchableReverseTool()
                });

            var summary = await runner.RunAsync(Path.Combine(solutionDirectory, "solution.json"), "abc");

            Assert.True(summary.Success, summary.Error);
            var serializedSummary = System.Text.Json.JsonSerializer.Serialize(summary);
            Assert.DoesNotContain(sensitiveKey, serializedSummary, StringComparison.Ordinal);

            var runRecordPath = Path.Combine(solutionDirectory, "runs", $"{summary.RunId}.records.json");
            Assert.True(File.Exists(runRecordPath));
            var recordJson = await File.ReadAllTextAsync(runRecordPath);
            Assert.DoesNotContain(sensitiveKey, recordJson, StringComparison.Ordinal);
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

            var policyPath = Path.Combine(solutionDirectory, "policy.json");
            await File.WriteAllTextAsync(policyPath,
                """
                {
                  "type": "allowall"
                }
                """);

            var solutionPath = Path.Combine(solutionDirectory, "solution.json");
            await File.WriteAllTextAsync(solutionPath,
                """
                {
                  "name": "replay-solution",
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

            var policyPath = Path.Combine(solutionDirectory, "policy.json");
            await File.WriteAllTextAsync(policyPath,
                """
                {
                  "type": "allowall"
                }
                """);

            var solutionPath = Path.Combine(solutionDirectory, "solution.json");
            await File.WriteAllTextAsync(solutionPath,
                """
                {
                  "name": "fork-solution",
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
        public IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Network];
        public ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Network);
        public Task<string> ExecuteAsync(string input)
        {
            WasCalled = true;
            return Task.FromResult($"EXFILTRATED: {input}");
        }
    }

    private sealed class ReverseTool : DispatchableToolBase
    {
        public override string Name => "reverse";
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input)
            => Task.FromResult(new string(input.Reverse().ToArray()));
    }

    private sealed class PrefixTool : DispatchableToolBase
    {
        private readonly string _prefix;
        public PrefixTool(string prefix) { _prefix = prefix; }
        public override string Name => "prefix";
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input) => Task.FromResult(_prefix + input);
    }

    private sealed class DispatchableReverseTool : DispatchableToolBase
    {
        public override string Name => "reverse";
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input)
            => Task.FromResult(new string(input.Reverse().ToArray()));
    }

    private sealed class ThrowIfCalledTool : DispatchableToolBase
    {
        public override string Name => "throw";
        public bool WasCalled { get; private set; }
        public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
        public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
        protected override Task<string> InvokeCoreAsync(string input)
        {
            WasCalled = true;
            throw new InvalidOperationException("Tool should not be called while replaying.");
        }
    }
}
