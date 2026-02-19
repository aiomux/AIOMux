using AIOMux.Core.Models;`nusing AIOMux.Core.Models.Agent;`nusing AIOMux.Core.Models.Events;`nusing AIOMux.Core.Models.Replay;
using AIOMux.Core.Replay;
using Xunit;

namespace AIOMux.Core.Tests.Replay;

public class RunRecorderTests : IDisposable
{
    private readonly string _testPath;
    private readonly RunRecorder _recorder;

    public RunRecorderTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"aiomux-replay-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);
        _recorder = new RunRecorder(_testPath);
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
    public async Task StartRunAsync_CreatesRunFile()
    {
        var run = new Run
        {
            PipelineName = "TestPipeline",
            PipelineVersion = "1.0.0"
        };

        var runId = await _recorder.StartRunAsync(run);

        Assert.NotNull(runId);
        Assert.Equal(run.RunId, runId);

        var filePath = Path.Combine(_testPath, $"{runId}.jsonl");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task RecordEventAsync_AppendsEvent()
    {
        var run = new Run { PipelineName = "Test" };
        await _recorder.StartRunAsync(run);

        var inputEvent = new InputReceivedEvent
        {
            Payload = new InputReceivedEvent.InputReceivedPayload
            {
                Input = "test input",
                InputHash = "ABC123"
            }
        };

        await _recorder.RecordEventAsync(inputEvent);

        // Finish the run to close the file
        await _recorder.FinishRunAsync(true);

        var filePath = Path.Combine(_testPath, $"{run.RunId}.jsonl");
        var lines = await File.ReadAllLinesAsync(filePath);

        // Should have RunStarted + InputReceived + RunFinished
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public async Task RecordEventAsync_EnforcesSequencing()
    {
        var run = new Run { PipelineName = "Test" };
        await _recorder.StartRunAsync(run);

        var event1 = new InputReceivedEvent
        {
            Payload = new InputReceivedEvent.InputReceivedPayload { Input = "1" }
        };
        var event2 = new StepCompletedEvent
        {
            Payload = new StepCompletedEvent.StepCompletedPayload { StepName = "Step1", Output = "out" }
        };

        await _recorder.RecordEventAsync(event1);
        await _recorder.RecordEventAsync(event2);

        // Finish the run to close the file
        await _recorder.FinishRunAsync(true);

        var filePath = Path.Combine(_testPath, $"{run.RunId}.jsonl");
        var lines = await File.ReadAllLinesAsync(filePath);

        Assert.Equal(4, lines.Length); // RunStarted + 2 events + RunFinished

        // Verify sequence numbers using JsonDocument
        var doc1 = System.Text.Json.JsonDocument.Parse(lines[1]);
        var doc2 = System.Text.Json.JsonDocument.Parse(lines[2]);

        Assert.Equal(1, doc1.RootElement.GetProperty("Seq").GetInt32());
        Assert.Equal(2, doc2.RootElement.GetProperty("Seq").GetInt32());
    }

    [Fact]
    public async Task FinishRunAsync_AddsRunFinishedEvent()
    {
        var run = new Run { PipelineName = "Test" };
        await _recorder.StartRunAsync(run);

        await _recorder.FinishRunAsync(
            success: true,
            finalOutput: "Final output",
            totalDurationMs: 123.45
        );

        var filePath = Path.Combine(_testPath, $"{run.RunId}.jsonl");
        var lines = await File.ReadAllLinesAsync(filePath);

        Assert.Equal(2, lines.Length); // RunStarted + RunFinished

        var lastLine = lines[^1];
        Assert.Contains("RunFinished", lastLine);
        Assert.Contains("Final output", lastLine);
    }

    [Fact]
    public async Task ListRunsAsync_ReturnsAllRuns()
    {
        // Create multiple runs
        var run1 = new Run { PipelineName = "Pipeline1" };
        var run2 = new Run { PipelineName = "Pipeline2" };

        await _recorder.StartRunAsync(run1);
        await _recorder.FinishRunAsync(true);

        await _recorder.StartRunAsync(run2);
        await _recorder.FinishRunAsync(false, error: "Test error");

        var runs = await _recorder.ListRunsAsync();

        Assert.Equal(2, runs.Count);
        Assert.Contains(runs, r => r.PipelineName == "Pipeline1");
        Assert.Contains(runs, r => r.PipelineName == "Pipeline2");
    }

    [Fact]
    public async Task GetRunMetadataAsync_ReturnsCorrectMetadata()
    {
        var run = new Run
        {
            PipelineName = "TestPipeline",
            PipelineVersion = "1.0.0"
        };

        await _recorder.StartRunAsync(run);
        await _recorder.RecordEventAsync(new InputReceivedEvent
        {
            Payload = new InputReceivedEvent.InputReceivedPayload { Input = "test" }
        });
        await _recorder.FinishRunAsync(success: true, finalOutput: "output");

        var metadata = await _recorder.GetRunMetadataAsync(run.RunId);

        Assert.NotNull(metadata);
        Assert.Equal(run.RunId, metadata.RunId);
        Assert.Equal("TestPipeline", metadata.PipelineName);
        Assert.True(metadata.Success);
        Assert.Equal(3, metadata.EventCount); // RunStarted + InputReceived + RunFinished
    }

    [Fact]
    public async Task DeterministicRecording_ProducesSameOutput()
    {
        var run1 = new Run { PipelineName = "Test", RunId = "test-run-1" };
        var run2 = new Run { PipelineName = "Test", RunId = "test-run-2" };

        // Record identical runs
        await _recorder.StartRunAsync(run1);
        await _recorder.RecordEventAsync(new InputReceivedEvent
        {
            Payload = new InputReceivedEvent.InputReceivedPayload
            {
                Input = "same input",
                InputHash = "HASH123"
            }
        });
        await _recorder.FinishRunAsync(true, "output");

        await _recorder.StartRunAsync(run2);
        await _recorder.RecordEventAsync(new InputReceivedEvent
        {
            Payload = new InputReceivedEvent.InputReceivedPayload
            {
                Input = "same input",
                InputHash = "HASH123"
            }
        });
        await _recorder.FinishRunAsync(true, "output");

        // Compare files (except runId and timestamps)
        var file1 = await File.ReadAllLinesAsync(Path.Combine(_testPath, "test-run-1.jsonl"));
        var file2 = await File.ReadAllLinesAsync(Path.Combine(_testPath, "test-run-2.jsonl"));

        Assert.Equal(file1.Length, file2.Length);

        // Both should have same event types using JsonDocument
        for (int i = 0; i < file1.Length; i++)
        {
            var doc1 = System.Text.Json.JsonDocument.Parse(file1[i]);
            var doc2 = System.Text.Json.JsonDocument.Parse(file2[i]);

            Assert.Equal(doc1.RootElement.GetProperty("Type").GetString(),
                        doc2.RootElement.GetProperty("Type").GetString());
            Assert.Equal(doc1.RootElement.GetProperty("Seq").GetInt32(),
                        doc2.RootElement.GetProperty("Seq").GetInt32());
        }
    }
}

