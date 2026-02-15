using AIOMux.Skills.Notes.Storage;
using AIOMux.Skills.Notes.Models;
using Xunit;

namespace AIOMux.Skills.Notes.Tests.Storage;

public class JsonNotesStoreTests : IDisposable
{
    private readonly string _testPath;
    private readonly JsonNotesStore _store;

    public JsonNotesStoreTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"aiomux-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);
        _store = new JsonNotesStore(_testPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testPath))
        {
            Directory.Delete(_testPath, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_CreatesNewNote()
    {
        var note = new NoteRecord
        {
            Title = "Test Note",
            Summary = "This is a test",
            Type = "note"
        };

        var id = await _store.SaveAsync(note);

        Assert.NotNull(id);
        Assert.Equal(note.Id, id);
    }

    [Fact]
    public async Task GetAsync_RetrievesNote()
    {
        var note = new NoteRecord
        {
            Title = "Test Note",
            Summary = "This is a test",
            Type = "note"
        };

        var id = await _store.SaveAsync(note);
        var retrieved = await _store.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal(note.Title, retrieved.Title);
        Assert.Equal(note.Summary, retrieved.Summary);
    }

    [Fact]
    public async Task SearchAsync_FindsByText()
    {
        await _store.SaveAsync(new NoteRecord
        {
            Title = "Performance Optimization",
            Summary = "Ideas for caching",
            Type = "idea"
        });

        await _store.SaveAsync(new NoteRecord
        {
            Title = "Meeting Notes",
            Summary = "Discussed project timeline",
            Type = "note"
        });

        var results = await _store.SearchAsync(new NoteQuery
        {
            Q = "caching"
        });

        Assert.Single(results);
        Assert.Contains("caching", results[0].Summary);
    }

    [Fact]
    public async Task SearchAsync_FiltersByType()
    {
        await _store.SaveAsync(new NoteRecord
        {
            Title = "Task 1",
            Type = "task"
        });

        await _store.SaveAsync(new NoteRecord
        {
            Title = "Note 1",
            Type = "note"
        });

        var results = await _store.SearchAsync(new NoteQuery
        {
            Type = "task"
        });

        Assert.Single(results);
        Assert.Equal("task", results[0].Type);
    }

    [Fact]
    public async Task SearchAsync_FiltersByTags()
    {
        await _store.SaveAsync(new NoteRecord
        {
            Title = "Note 1",
            Tags = new List<string> { "performance", "caching" }
        });

        await _store.SaveAsync(new NoteRecord
        {
            Title = "Note 2",
            Tags = new List<string> { "security" }
        });

        var results = await _store.SearchAsync(new NoteQuery
        {
            Tags = new List<string> { "performance" }
        });

        Assert.Single(results);
        Assert.Contains("performance", results[0].Tags);
    }

    [Fact]
    public async Task PersistenceAcrossInstances()
    {
        // Save with first instance
        var note = new NoteRecord
        {
            Title = "Persisted Note",
            Summary = "This should persist"
        };
        await _store.SaveAsync(note);

        // Create new instance and verify
        var newStore = new JsonNotesStore(_testPath);
        var retrieved = await newStore.GetAsync(note.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(note.Title, retrieved.Title);
    }

    [Fact]
    public async Task ConcurrentAccess_ThreadSafe()
    {
        var tasks = Enumerable.Range(0, 10).Select(i => Task.Run(async () =>
        {
            var note = new NoteRecord
            {
                Title = $"Note {i}",
                Summary = $"Content {i}"
            };
            await _store.SaveAsync(note);
        }));

        await Task.WhenAll(tasks);

        var all = await _store.GetAllAsync();
        Assert.Equal(10, all.Count);
    }
}
