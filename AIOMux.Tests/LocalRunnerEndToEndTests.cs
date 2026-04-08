using AIOMux.Local;

namespace AIOMux.Tests;

public sealed class LocalRunnerEndToEndTests
{
    [Fact]
    public async Task ValidateAndRun_WithBuiltInConnectorAndAgent_CompletesSuccessfully()
    {
        var solutionDirectory = Path.Combine(Path.GetTempPath(), "aiomux-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(solutionDirectory);

        try
        {
            var planPath = Path.Combine(solutionDirectory, "plan.json");
            await File.WriteAllTextAsync(planPath,
                """
                {
                  "name": "console-echo-plan",
                  "steps": [
                    {
                      "id": "echo",
                      "type": "agent",
                      "target": "echo"
                    }
                  ]
                }
                """);

            var solutionPath = Path.Combine(solutionDirectory, "solution.json");
            await File.WriteAllTextAsync(solutionPath,
                """
                {
                  "name": "console-echo",
                  "entry": "plan.json",
                  "entryAgent": "echo",
                  "connectors": [
                    {
                      "type": "console",
                      "name": "console-input",
                      "config": {
                        "channel": "demo"
                      }
                    }
                  ],
                  "assemblies": []
                }
                """);

            var validator = new SolutionValidator();
            await validator.ValidateAsync(solutionPath);

            var runner = new SolutionRunner();
            var loaded = await runner.LoadAsync(solutionPath);

            Assert.Single(loaded.Connectors);
            Assert.Equal("console", loaded.Connectors[0].Declaration.Type);
            Assert.Equal("demo", loaded.Connectors[0].Declaration.Config["channel"]);

            var summary = await runner.RunAsync(solutionPath, "hello from e2e");

            Assert.True(summary.Success, summary.Error);
            Assert.Null(summary.Error);
            Assert.Equal(1, summary.ExecutedSteps);
            Assert.Equal(1, summary.StepCount);
            Assert.Contains("hello from e2e", summary.Output ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }
}
