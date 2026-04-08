using AIOMux.Core.Interfaces;
using AIOMux.Local;

namespace AIOMux.Tests;

public sealed class SolutionRunnerIntegrationTests
{
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
        }
        finally
        {
            if (Directory.Exists(solutionDirectory))
                Directory.Delete(solutionDirectory, recursive: true);
        }
    }

    private sealed class ReverseTool : ITool
    {
        public string Name => "reverse";

        public Task<string> ExecuteAsync(string input)
            => Task.FromResult(new string(input.Reverse().ToArray()));
    }
}
