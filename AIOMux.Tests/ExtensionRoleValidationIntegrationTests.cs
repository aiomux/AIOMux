using AIOMux.Local;
using System.Text.Json;

namespace AIOMux.Tests;

public sealed class ExtensionRoleValidationIntegrationTests
{
    [Fact]
    public async Task LoadAsync_WithToolPackageContainingAgent_ThrowsRoleValidationError()
    {
        var solutionDirectory = CreateTempDirectory();

        try
        {
            var packagePath = typeof(ExtensionRoleValidationIntegrationTests).Assembly.Location;
            await WritePlanAsync(solutionDirectory);
            await WritePolicyAsync(solutionDirectory);
            var solutionPath = Path.Combine(solutionDirectory, "solution.json");

            await WriteJsonAsync(solutionPath, new
            {
                name = "role-invalid-tool",
                entry = "plan.json",
                policyConfig = "policy.json",
                agents = Array.Empty<string>(),
                tools = new[] { packagePath },
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
            var packagePath = typeof(ExtensionRoleValidationIntegrationTests).Assembly.Location;
            await WritePlanAsync(solutionDirectory);
            await WritePolicyAsync(solutionDirectory);
            var solutionPath = Path.Combine(solutionDirectory, "solution.json");

            await WriteJsonAsync(solutionPath, new
            {
                name = "role-invalid-agent",
                entry = "plan.json",
                policyConfig = "policy.json",
                agents = new[] { packagePath },
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

    private static async Task WritePlanAsync(string solutionDirectory)
    {
        await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
            """
            {
              "name": "role-validation-plan",
              "steps": []
            }
            """);
    }

    [Fact]
    public async Task LoadAsync_WithConnectorPackageContainingAgent_ThrowsRoleValidationError()
    {
        var solutionDirectory = CreateTempDirectory();

        try
        {
            var packagePath = typeof(ExtensionRoleValidationIntegrationTests).Assembly.Location;
            await WritePlanAsync(solutionDirectory);
            await WritePolicyAsync(solutionDirectory);
            var solutionPath = Path.Combine(solutionDirectory, "solution.json");

            await WriteJsonAsync(solutionPath, new
            {
                name = "role-invalid-connector",
                entry = "plan.json",
                policyConfig = "policy.json",
                agents = Array.Empty<string>(),
                tools = Array.Empty<string>(),
                connectors = new[] { packagePath },
                connectorConfigurations = new[]
                {
                    new { type = "console", name = "input" }
                },
                executionOptions = new
                {
                    collectMetrics = false,
                    generateJobSummary = false,
                    includeDetailedMetrics = false
                }
            });

            var runner = new SolutionRunner();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.LoadAsync(solutionPath));

            Assert.Contains("Connector package", ex.Message, StringComparison.Ordinal);
            Assert.Contains("agent implementations", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(solutionDirectory);
        }
    }

    private static async Task WritePolicyAsync(string solutionDirectory)
    {
        await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "policy.json"),
            """
            {
              "type": "allowall"
            }
            """);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "aiomux-role-validation-" + Guid.NewGuid().ToString("N"));
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
