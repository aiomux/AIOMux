using AIOMux.Core.Models;
using AIOMux.Core.Replay;
using Xunit;

namespace AIOMux.Core.Tests.Replay;

public class ReplayEngineTests : IDisposable
{
    private readonly string _testPath;
    private readonly RunRecorder _recorder;
    private readonly ReplayEngine _engine;

    public ReplayEngineTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"aiomux-replay-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);
        _recorder = new RunRecorder(_testPath);
        _engine = new ReplayEngine(_testPath);
    }

    public void Dispose()
    {
        _recorder.Dispose();
        if (Directory.Exists(_testPath))
        {
            Directory.Delete(_testPath, recursive: true);
        }
    }

    [Fact]
    public async Task ReplayAsync_ReconstructsSimpleRun()
    {
        // Record a simple run
        var run = new Run { PipelineName = "TestPipeline" };
        await _recorder.StartRunAsync(run);

        await _recorder.RecordEventAsync(new InputReceivedEvent
        {
            Payload = new InputReceivedEvent.InputReceivedPayload
            {
                Input = "test input",
                InputHash = "HASH"
            }
        });

        await _recorder.RecordEventAsync(new StepCompletedEvent
        {
            Payload = new StepCompletedEvent.StepCompletedPayload
            {
                StepName = "Step1",
                Output = "step output",
                DurationMs = 10.5
            }
        });

        await _recorder.FinishRunAsync(success: true, finalOutput: "final output", totalDurationMs: 50.0);

        // Replay
        var result = await _engine.ReplayAsync(run.RunId);

        Assert.True(result.Success);
        Assert.Equal(run.RunId, result.RunId);
        Assert.Equal("TestPipeline", result.PipelineName);
        Assert.Equal("test input", result.Input);
        Assert.Equal("final output", result.FinalOutput);
        Assert.Single(result.Steps);
        Assert.Equal("Step1", result.Steps[0].StepName);
        Assert.Equal("step output", result.Steps[0].Output);
    }

    [Fact]
    public async Task ReplayAsync_ReconstructsToolCalls()
    {
        var run = new Run { PipelineName = "TestPipeline" };
        await _recorder.StartRunAsync(run);

        await _recorder.RecordEventAsync(new ToolInvokedEvent
        {
            Payload = new ToolInvokedEvent.ToolInvokedPayload
            {
                ToolName = "TestTool",
                Args = "{\"key\":\"value\"}",
                ArgsHash = "HASH"
            }
        });

        await _recorder.RecordEventAsync(new AIOMux.Core.Models.ToolResultEvent
        {
            Payload = new AIOMux.Core.Models.ToolResultEvent.ToolResultPayload
            {
                ToolName = "TestTool",
                Result = "tool result",
                DurationMs = 25.3,
                Success = true
            }
        });

        await _recorder.FinishRunAsync(success: true);

        // Replay
        var result = await _engine.ReplayAsync(run.RunId);

        Assert.True(result.Success);
        Assert.Single(result.ToolCalls);
        Assert.Equal("TestTool", result.ToolCalls[0].ToolName);
        Assert.Equal("{\"key\":\"value\"}", result.ToolCalls[0].Args);
        Assert.Equal("tool result", result.ToolCalls[0].Result);
        Assert.True(result.ToolCalls[0].Success);
    }

    [Fact]
    public async Task ReplayAsync_ValidatesOrdering()
    {
        var run = new Run { PipelineName = "Test" };
        var runId = await _recorder.StartRunAsync(run);

        // Manually create a file with bad ordering
        var filePath = Path.Combine(_testPath, $"{runId}.jsonl");
        await File.AppendAllTextAsync(filePath,
            System.Text.Json.JsonSerializer.Serialize(new InputReceivedEvent
            {
                RunId = runId,
                Seq = 99, // Wrong sequence
                Type = "InputReceived",
                Payload = new InputReceivedEvent.InputReceivedPayload
                {
                    Input = "test"
                }
            }) + "\n"
        );

        // Replay should fail
        var result = await _engine.ReplayAsync(runId);

        Assert.False(result.Success);
        Assert.Contains("ordering", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplayAsync_ValidatesHashes()
    {
        var run = new Run { PipelineName = "Test" };
        await _recorder.StartRunAsync(run);

        var inputEvent = new InputReceivedEvent
        {
            Payload = new InputReceivedEvent.InputReceivedPayload
            {
                Input = "test",
                InputHash = "HASH"
            }
        };

        await _recorder.RecordEventAsync(inputEvent);
        await _recorder.FinishRunAsync(true);

        // Manually tamper with the file using string manipulation
        var filePath = Path.Combine(_testPath, $"{run.RunId}.jsonl");
        var lines = await File.ReadAllLinesAsync(filePath);

        // Tamper with the PayloadHash property in the second line (InputReceived event)
        // Find the PayloadHash and change it
        var doc = System.Text.Json.JsonDocument.Parse(lines[1]);
        var originalHash = doc.RootElement.GetProperty("PayloadHash").GetString();

        // Replace the hash with a wrong one
        lines[1] = lines[1].Replace($"\"{originalHash}\"", "\"TAMPERED_HASH\"");
        await File.WriteAllLinesAsync(filePath, lines);

        // Replay with hash validation should detect tampering
        var result = await _engine.ReplayAsync(run.RunId, validateHashes: true);

        Assert.False(result.Success);
        Assert.Contains("Hash", result.Error);
    }

    [Fact]
    public async Task ReplayAsync_HandlesIncompleteRuns()
    {
        var run = new Run { PipelineName = "Incomplete" };
        await _recorder.StartRunAsync(run);

        await _recorder.RecordEventAsync(new InputReceivedEvent
        {
            Payload = new InputReceivedEvent.InputReceivedPayload { Input = "test" }
        });

        // Don't call FinishRunAsync - simulate incomplete run

        // Replay should still work
        var result = await _engine.ReplayAsync(run.RunId);

        Assert.True(result.Success);
        Assert.Null(result.FinalOutput);
        Assert.Null(result.EndTime);
    }

    [Fact]
    public async Task ReplayAsync_ReproducesStepOutputsExactly()
    {
        var run = new Run { PipelineName = "Deterministic" };
        await _recorder.StartRunAsync(run);

        var steps = new[]
        {
            ("Normalize", "normalized text"),
            ("Classify", "task"),
            ("Summarize", "summary text")
        };

        foreach (var (name, output) in steps)
        {
            await _recorder.RecordEventAsync(new StepCompletedEvent
            {
                Payload = new StepCompletedEvent.StepCompletedPayload
                {
                    StepName = name,
                    Output = output,
                    DurationMs = 1.0
                }
            });
        }

        await _recorder.FinishRunAsync(true);

        // Replay
        var result = await _engine.ReplayAsync(run.RunId);

        Assert.True(result.Success);
        Assert.Equal(3, result.Steps.Count);

        for (int i = 0; i < steps.Length; i++)
        {
            Assert.Equal(steps[i].Item1, result.Steps[i].StepName);
            Assert.Equal(steps[i].Item2, result.Steps[i].Output);
        }
    }

    [Fact]
    public async Task ReplayAsync_ReturnsErrorForMissingRun()
    {
        var result = await _engine.ReplayAsync("nonexistent-run-id");

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }
}
