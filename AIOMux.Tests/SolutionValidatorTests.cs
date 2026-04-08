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
            await WritePlanAsync(solutionDirectory, "echo");
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "invalid-duplicate-connectors",
                  "entry": "plan.json",
                  "entryAgent": "echo",
                  "connectors": [
                    { "type": "console", "name": "dup" },
                    { "type": "console", "name": "dup" }
                  ],
                  "assemblies": []
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
            await WritePlanAsync(solutionDirectory, "echo");
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "invalid-connector-type",
                  "entry": "plan.json",
                  "entryAgent": "echo",
                  "connectors": [
                    { "type": "does-not-exist", "name": "input" }
                  ],
                  "assemblies": []
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
            await WritePlanAsync(solutionDirectory, "does-not-exist");
            await File.WriteAllTextAsync(Path.Combine(solutionDirectory, "solution.json"),
                """
                {
                  "name": "invalid-agent",
                  "entry": "plan.json",
                  "connectors": [
                    { "type": "console", "name": "input" }
                  ],
                  "assemblies": []
                }
                """);

            var validator = new SolutionValidator();
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                validator.ValidateAsync(Path.Combine(solutionDirectory, "solution.json")));

            Assert.Contains("not available as a built-in agent", ex.Message, StringComparison.OrdinalIgnoreCase);
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

    private static void DeleteDirectory(string solutionDirectory)
    {
        if (Directory.Exists(solutionDirectory))
            Directory.Delete(solutionDirectory, recursive: true);
    }
}
