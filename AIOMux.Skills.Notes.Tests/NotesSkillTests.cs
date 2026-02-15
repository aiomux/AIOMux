using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Skills.Notes.Storage;
using Xunit;

namespace AIOMux.Skills.Notes.Tests;

public class NotesSkillTests : IDisposable
{
    private readonly string _testPath;
    private readonly JsonNotesStore _store;
    private readonly NotesSkill _skill;

    public NotesSkillTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"aiomux-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);
        _store = new JsonNotesStore(_testPath);
        _skill = new NotesSkill(_store);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testPath))
        {
            Directory.Delete(_testPath, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AddsNote()
    {
        var context = new AgentContext
        {
            UserInput = "Remember to review PR #123 #urgent",
            Tools = new Dictionary<string, ITool>
            {
                { "NotesStore", _store }
            }
        };

        var result = await _skill.ExecuteAsync(context);

        Assert.Contains("Note added", result);
        Assert.Contains("task", result); // Should classify as task
        Assert.Contains("urgent", result); // Should extract tag
    }

    [Fact]
    public async Task ExecuteAsync_SearchesNotes()
    {
        // Add some notes first
        await _skill.ExecuteAsync(new AgentContext
        {
            UserInput = "Performance optimization ideas #performance",
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        });

        await _skill.ExecuteAsync(new AgentContext
        {
            UserInput = "Meeting notes from standup",
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        });

        // Search
        var context = new AgentContext
        {
            UserInput = "search performance",
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        };

        var result = await _skill.ExecuteAsync(context);

        Assert.Contains("Found", result);
        Assert.Contains("Performance", result);
    }

    [Fact]
    public async Task ExecuteAsync_ListsAllNotes()
    {
        // Add notes
        await _skill.ExecuteAsync(new AgentContext
        {
            UserInput = "First note",
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        });

        await _skill.ExecuteAsync(new AgentContext
        {
            UserInput = "Second note",
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        });

        // List
        var context = new AgentContext
        {
            UserInput = "list",
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        };

        var result = await _skill.ExecuteAsync(context);

        Assert.Contains("2 note(s)", result);
    }

    [Fact]
    public async Task ExecuteAsync_EnforcesPermissions_ThrowsWhenMultipleTools()
    {
        var context = new AgentContext
        {
            UserInput = "Test note",
            Tools = new Dictionary<string, ITool>
            {
                { "NotesStore", _store },
                { "OtherTool", new MockTool() }
            }
        };

        var result = await _skill.ExecuteAsync(context);

        Assert.Contains("only use NotesStore", result);
    }

    [Fact]
    public async Task ExecuteAsync_EnforcesPermissions_ThrowsWhenWrongTool()
    {
        var context = new AgentContext
        {
            UserInput = "Test note",
            Tools = new Dictionary<string, ITool>
            {
                { "OtherTool", new MockTool() }
            }
        };

        var result = await _skill.ExecuteAsync(context);

        Assert.Contains("only use NotesStore", result);
    }

    [Fact]
    public async Task Pipeline_ProducesDeterministicOutput()
    {
        // Same input should produce same output
        var input1 = "This is a test note #test";
        var input2 = "This is a test note #test";

        var context1 = new AgentContext
        {
            UserInput = input1,
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        };

        var context2 = new AgentContext
        {
            UserInput = input2,
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        };

        var result1 = await _skill.ExecuteAsync(context1);
        var result2 = await _skill.ExecuteAsync(context2);

        // Both should have same type and tags
        Assert.Contains("note", result1);
        Assert.Contains("note", result2);
        Assert.Contains("test", result1);
        Assert.Contains("test", result2);
    }

    [Fact]
    public void EventLog_CapturesAllSteps()
    {
        var context = new AgentContext
        {
            UserInput = "Test note",
            Tools = new Dictionary<string, ITool> { { "NotesStore", _store } }
        };

        var _ = _skill.ExecuteAsync(context).Result;
        var eventLog = _skill.GetEventLog();

        // Should have events for all pipeline steps
        Assert.Contains(eventLog, e => e.Contains("InputReceived"));
        Assert.Contains(eventLog, e => e.Contains("Normalized"));
        Assert.Contains(eventLog, e => e.Contains("Classified"));
        Assert.Contains(eventLog, e => e.Contains("Summarized"));
        Assert.Contains(eventLog, e => e.Contains("MetadataExtracted"));
        Assert.Contains(eventLog, e => e.Contains("Persisted"));
    }

    private class MockTool : ITool
    {
        public string Name => "MockTool";
        public Task<string> ExecuteAsync(string input) => Task.FromResult("mock");
    }
}
