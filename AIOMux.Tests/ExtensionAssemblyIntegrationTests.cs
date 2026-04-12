using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Local;
using System.Collections.Immutable;
using System.Text.Json;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests;

public sealed class ExtensionAssemblyIntegrationTests
{
    [Fact]
    public async Task RunAsync_WithAssemblyProvidedTool_ExecutesToolStep()
    {
        var solutionDirectory = CreateTempDirectory();

        try
        {
            var assemblyPath = typeof(ExtensionAssemblyIntegrationTests).Assembly.Location;
            await WriteJsonAsync(Path.Combine(solutionDirectory, "plan.json"), new
            {
                name = "assembly-tool-plan",
                steps = new object[]
                {
                    new
                    {
                        id = "tool-step",
                        type = "tool",
                        target = "assembly-uppercase",
                        bindings = new Dictionary<string, string>
                        {
                            ["input"] = "inputs.input"
                        },
                        outputKey = "toolOutput"
                    }
                }
            });

            var solutionPath = Path.Combine(solutionDirectory, "solution.json");
            await WriteJsonAsync(solutionPath, new
            {
                name = "assembly-tool-solution",
                entry = "plan.json",
                assemblies = new[] { assemblyPath },
                executionOptions = new
                {
                    collectMetrics = false,
                    generateJobSummary = false,
                    includeDetailedMetrics = false
                }
            });

            var validator = new SolutionValidator();
            await validator.ValidateAsync(solutionPath);

            var runner = new SolutionRunner();
            var summary = await runner.RunAsync(solutionPath, "hello from assembly tool");

            Assert.True(summary.Success, summary.Error);
            Assert.Equal("HELLO FROM ASSEMBLY TOOL", summary.Output);
            Assert.Equal(1, summary.ExecutedSteps);
            Assert.NotNull(summary.RunId);
        }
        finally
        {
            DeleteDirectory(solutionDirectory);
        }
    }

    [Fact]
    public async Task LoadAsync_WithAssemblyProvidedAgent_RegistersAgentAndRuntimeExecutesPlan()
    {
        var solutionDirectory = CreateTempDirectory();

        try
        {
            var assemblyPath = typeof(ExtensionAssemblyIntegrationTests).Assembly.Location;
            await WriteJsonAsync(Path.Combine(solutionDirectory, "plan.json"), new
            {
                name = "assembly-agent-plan",
                steps = new object[]
                {
                    new
                    {
                        id = "agent-step",
                        type = "agent",
                        target = "assembly-prefix",
                        bindings = new Dictionary<string, string>
                        {
                            ["input"] = "inputs.input"
                        },
                        outputKey = "agentOutput"
                    }
                }
            });

            var solutionPath = Path.Combine(solutionDirectory, "solution.json");
            await WriteJsonAsync(solutionPath, new
            {
                name = "assembly-agent-solution",
                entry = "plan.json",
                assemblies = new[] { assemblyPath },
                executionOptions = new
                {
                    collectMetrics = false,
                    generateJobSummary = false,
                    includeDetailedMetrics = false
                }
            });

            var runner = new SolutionRunner();
            var loaded = await runner.LoadAsync(solutionPath);
            var agent = loaded.Services.AgentManager?.GetByName("assembly-prefix");

            Assert.NotNull(agent);

            var context = new ExecutionContext
            {
                Services = loaded.Services,
                WorkingDirectory = loaded.WorkingDirectory
            };
            context.Inputs["input"] = "hello from assembly agent";

            var runtime = new ExecutionRuntime();
            var result = await runtime.ExecuteAsync(loaded.Plan, context);

            Assert.True(result.Success, result.Error);
            Assert.Equal("[ASSEMBLY-PREFIX] hello from assembly agent", result.Output);
            Assert.Equal("[ASSEMBLY-PREFIX] hello from assembly agent", context.State["agentOutput"]);
        }
        finally
        {
            DeleteDirectory(solutionDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "aiomux-assembly-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WriteJsonAsync(string path, object value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
}

public sealed class AssemblyUppercaseTool : ITool
{
    public string Name => "assembly-uppercase";

    public Task<string> ExecuteAsync(string input)
        => Task.FromResult(input.ToUpperInvariant());
}

public sealed class AssemblyPrefixAgent : IAgent
{
    public string Name => "assembly-prefix";

    public string Description => "Prefixes input for assembly-based integration tests.";

    public Task<StepExecutionResult> ExecuteAsync(
        ImmutableDictionary<string, object?> inputs,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var value = inputs.TryGetValue("input", out var input)
            ? input?.ToString() ?? string.Empty
            : context.GetInput();

        return Task.FromResult(new StepExecutionResult
        {
            Success = true,
            Output = $"[ASSEMBLY-PREFIX] {value}"
        });
    }
}
