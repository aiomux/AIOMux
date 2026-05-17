using AIOMux.Connectors.Console;
using AIOMux.Local;

namespace AIOMux.Tests;

public sealed class SolutionValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WithDuplicateConnectorNames_ThrowsClearError()
    {
        var solutionDirectory = CreateTempDirectory();

        try
        {
            var connectorPath = GetConnectorPackagePath();
            await WritePlanAsync(solutionDirectory, "echo");
            await WritePolicyAsync(solutionDirectory);
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                $$"""
                {
                  "name": "invalid-duplicate-connectors",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "entryAgent": "echo",
                  "connectorConfigurations": [
                    { "type": "console", "name": "dup" },
                    { "type": "console", "name": "dup" }
                  ],
                  "agents": [],
                  "tools": [],
                  "connectors": ["{{connectorPath.Replace("\\", "\\\\")}}"]
                }
                """);

            var validator = new SolutionValidator();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                validator.ValidateAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("Connector names must be unique", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(solutionDirectory);
        }
    }

    [Fact]
    public async Task ValidateAsync_WithUnknownConnectorType_ThrowsClearError()
    {
        var solutionDirectory = CreateTempDirectory();

        try
        {
            var connectorPath = GetConnectorPackagePath();
            await WritePlanAsync(solutionDirectory, "echo");
            await WritePolicyAsync(solutionDirectory);
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                $$"""
                {
                  "name": "invalid-connector-type",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "entryAgent": "echo",
                  "connectorConfigurations": [
                    { "type": "does-not-exist", "name": "input" }
                  ],
                  "agents": [],
                  "tools": [],
                  "connectors": ["{{connectorPath.Replace("\\", "\\\\")}}"]
                }
                """);

            var validator = new SolutionValidator();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                validator.ValidateAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("unknown type", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(solutionDirectory);
        }
    }

    [Fact]
    public async Task ValidateAsync_WithUnknownAgentInPlan_ThrowsClearError()
    {
        var solutionDirectory = CreateTempDirectory();

        try
        {
            var connectorPath = GetConnectorPackagePath();
            await WritePlanAsync(solutionDirectory, "does-not-exist");
            await WritePolicyAsync(solutionDirectory);
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                $$"""
                {
                  "name": "invalid-agent",
                  "entry": "plan.json",
                  "policyConfig": "policy.json",
                  "connectorConfigurations": [
                    { "type": "console", "name": "input" }
                  ],
                  "agents": [],
                  "tools": [],
                  "connectors": ["{{connectorPath.Replace("\\", "\\\\")}}"]
                }
                """);

            var validator = new SolutionValidator();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                validator.ValidateAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("not available as a built-in or extension agent", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteDirectory(solutionDirectory);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "aiomux-validator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static async Task WritePlanAsync(string solutionDirectory, string agentTarget)
    {
        await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "plan.json"),
            $$"""
            {
              "name": "test-plan",
              "steps": [
                {
                  "id": "step1",
                  "type": "agent",
                  "target": "{{agentTarget}}"
                }
              ]
            }
            """);
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

    private static string GetConnectorPackagePath() => typeof(ConsoleConnector).Assembly.Location;

    private static void DeleteDirectory(string solutionDirectory)
    {
        if (Directory.Exists(solutionDirectory))
            Directory.Delete(solutionDirectory, recursive: true);
    }
}
