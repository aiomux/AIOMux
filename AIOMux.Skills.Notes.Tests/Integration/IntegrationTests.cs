using AIOMux.Skills.Notes;
using AIOMux.Skills.Notes.Storage;
using Xunit;

namespace AIOMux.Skills.Notes.Tests.Integration;

/// <summary>
/// Integration tests that verify the full pipeline and CLI behavior.
/// </summary>
public class IntegrationTests : IDisposable
{
    private readonly string _testPath;

    public IntegrationTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"aiomux-integration-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testPath))
        {
            Directory.Delete(_testPath, recursive: true);
        }
    }

    [Fact]
    public async Task EndToEnd_AddSearchAndList()
    {
        var store = new JsonNotesStore(_testPath);
        var skill = new NotesSkill(store);

        // Add multiple notes
        await AddNote(skill, store, "Remember to review PR #123 #urgent");
        await AddNote(skill, store, "Idea: implement caching layer #performance");
        await AddNote(skill, store, "Meeting notes from standup");

        // Verify search
        var searchResult = await Search(skill, store, "caching");
        Assert.Contains("caching", searchResult);
        Assert.Contains("idea", searchResult.ToLowerInvariant());

        // Verify list
        var listResult = await List(skill, store);
        Assert.Contains("3 note(s)", listResult);
    }

    [Fact]
    public async Task EndToEnd_PermissionEnforcement()
    {
        var store = new JsonNotesStore(_testPath);
        var skill = new NotesSkill(store);

        // Attempt to add note with wrong tools
        var context = new Core.AgentContext
        {
            UserInput = "Test note",
            Tools = new Dictionary<string, Core.Interfaces.ITool>
            {
                { "WrongTool", new MockTool() }
            }
        };

        var result = await skill.ExecuteAsync(context);

        Assert.Contains("only use NotesStore", result);
    }

    [Fact]
    public async Task EndToEnd_MetadataExtraction()
    {
        var store = new JsonNotesStore(_testPath);
        var skill = new NotesSkill(store);

        var result = await AddNote(skill, store,
            "Discussed with @Alice about #performance improvements for the Seattle office https://example.com/perf");

        // Verify all metadata was extracted
        Assert.Contains("performance", result);  // Tag
        Assert.Contains("Alice", result);        // @mention
        Assert.Contains("https://example.com/perf", result);  // URL
        Assert.Contains("Seattle", result);      // Proper noun
    }

    [Fact]
    public async Task EndToEnd_Classification()
    {
        var store = new JsonNotesStore(_testPath);
        var skill = new NotesSkill(store);

        // Test task
        var taskResult = await AddNote(skill, store, "TODO: review code");
        Assert.Contains("task", taskResult);

        // Test idea
        var ideaResult = await AddNote(skill, store, "Idea: use GraphQL");
        Assert.Contains("idea", ideaResult);

        // Test reference
        var refResult = await AddNote(skill, store, "Check https://docs.com");
        Assert.Contains("reference", refResult);

        // Test note
        var noteResult = await AddNote(skill, store, "Regular meeting notes");
        Assert.Contains("note", noteResult);
    }

    [Fact]
    public async Task EndToEnd_PersistenceAcrossSessions()
    {
        // Session 1: Add notes
        {
            var store = new JsonNotesStore(_testPath);
            var skill = new NotesSkill(store);
            await AddNote(skill, store, "Persistent note #test");
        }

        // Session 2: Search notes
        {
            var store = new JsonNotesStore(_testPath);
            var skill = new NotesSkill(store);
            var result = await Search(skill, store, "Persistent");
            Assert.Contains("Persistent note", result);
        }
    }

    [Fact]
    public async Task EndToEnd_EventLogging()
    {
        var store = new JsonNotesStore(_testPath);
        var skill = new NotesSkill(store);

        await AddNote(skill, store, "Test note");

        var events = skill.GetEventLog();

        // Verify all pipeline steps were logged
        Assert.Contains(events, e => e.Contains("InputReceived"));
        Assert.Contains(events, e => e.Contains("Normalized"));
        Assert.Contains(events, e => e.Contains("Classified"));
        Assert.Contains(events, e => e.Contains("Summarized"));
        Assert.Contains(events, e => e.Contains("MetadataExtracted"));
        Assert.Contains(events, e => e.Contains("Persisted"));

        // Verify events are valid JSON
        foreach (var evt in events)
        {
            var parsed = System.Text.Json.JsonDocument.Parse(evt);
            Assert.NotNull(parsed.RootElement.GetProperty("step"));
            Assert.NotNull(parsed.RootElement.GetProperty("timestamp"));
        }
    }

    [Fact]
    public async Task EndToEnd_DeterministicPipeline()
    {
        var store1 = new JsonNotesStore(_testPath + "_1");
        var skill1 = new NotesSkill(store1);

        var store2 = new JsonNotesStore(_testPath + "_2");
        var skill2 = new NotesSkill(store2);

        var input = "Test note #tag @Person https://example.com";

        var result1 = await AddNote(skill1, store1, input);
        var result2 = await AddNote(skill2, store2, input);

        // Results should be identical (except IDs)
        var events1 = skill1.GetEventLog();
        var events2 = skill2.GetEventLog();

        Assert.Equal(events1.Count, events2.Count);

        // Cleanup
        Directory.Delete(_testPath + "_1", recursive: true);
        Directory.Delete(_testPath + "_2", recursive: true);
    }

    private async Task<string> AddNote(NotesSkill skill, JsonNotesStore store, string text)
    {
        var context = new Core.AgentContext
        {
            UserInput = text,
            Tools = new Dictionary<string, Core.Interfaces.ITool>
            {
                { "NotesStore", store }
            }
        };

        return await skill.ExecuteAsync(context);
    }

    private async Task<string> Search(NotesSkill skill, JsonNotesStore store, string query)
    {
        var context = new Core.AgentContext
        {
            UserInput = $"search {query}",
            Tools = new Dictionary<string, Core.Interfaces.ITool>
            {
                { "NotesStore", store }
            }
        };

        return await skill.ExecuteAsync(context);
    }

    private async Task<string> List(NotesSkill skill, JsonNotesStore store)
    {
        var context = new Core.AgentContext
        {
            UserInput = "list",
            Tools = new Dictionary<string, Core.Interfaces.ITool>
            {
                { "NotesStore", store }
            }
        };

        return await skill.ExecuteAsync(context);
    }

    private class MockTool : Core.Interfaces.ITool
    {
        public string Name => "MockTool";
        public Task<string> ExecuteAsync(string input) => Task.FromResult("mock");
    }
}
