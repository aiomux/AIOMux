using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Local;
using System.Collections.Immutable;
using System.Text.Json;
using ExecutionContext = AIOMux.Core.Models.ExecutionContext;

namespace AIOMux.Tests;

public sealed class ExtensionAssemblyIntegrationTests
{
    [Fact]
    public async Task RunAsync_WithToolPackageContainingAgent_ThrowsRoleValidationError()
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
            await WriteJsonAsync(Path.Combine(solutionDirectory, "policy.json"), new
            {
                type = "allowall",
                parameters = new { }
            });
            await WriteJsonAsync(solutionPath, new
            {
                name = "assembly-tool-solution",
                entry = "plan.json",
                policyConfig = "policy.json",
                agents = Array.Empty<string>(),
                tools = new[] { assemblyPath },
                connectors = Array.Empty<string>(),
                executionOptions = new
                {
                    collectMetrics = false,
                    generateJobSummary = false,
                    includeDetailedMetrics = false
                }
            });

            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.RunAsync(solutionPath, "hello from assembly tool"));

            Assert.Contains("Tool package", ex.Message, StringComparison.Ordinal);
            Assert.Contains("agent implementations", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(solutionDirectory);
        }
    }

    [Fact]
    public async Task LoadAsync_WithAgentPackageContainingTool_ThrowsRoleValidationError()
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
            await WriteJsonAsync(Path.Combine(solutionDirectory, "policy.json"), new
            {
                type = "allowall",
                parameters = new { }
            });
            await WriteJsonAsync(solutionPath, new
            {
                name = "assembly-agent-solution",
                entry = "plan.json",
                policyConfig = "policy.json",
                agents = new[] { assemblyPath },
                tools = Array.Empty<string>(),
                connectors = Array.Empty<string>(),
                executionOptions = new
                {
                    collectMetrics = false,
                    generateJobSummary = false,
                    includeDetailedMetrics = false
                }
            });

            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.LoadAsync(solutionPath));

            Assert.Contains("Agent package", ex.Message, StringComparison.Ordinal);
            Assert.Contains("tool implementations", ex.Message, StringComparison.OrdinalIgnoreCase);
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

public sealed class AssemblyUppercaseTool : DispatchableToolBase
{
    public override string Name => "assembly-uppercase";
    public override IReadOnlyCollection<ToolOperation> SupportedOperations { get; } = [ToolOperation.Read];
    public override ToolExecutionAnalysis Analyze(string input) => ToolExecutionAnalysis.Recognized(ToolOperation.Read);
    protected override Task<string> InvokeCoreAsync(string input) => Task.FromResult(input.ToUpperInvariant());
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
