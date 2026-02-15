using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Skills.Notes.Storage;

namespace AIOMux.Skills.Notes;

/// <summary>
/// Plugin implementation for NotesSkill.
/// Handles initialization and agent creation.
/// </summary>
public class NotesSkillPlugin : IAgentPlugin
{
    private INotesStore? _store;

    public AgentMetadata Metadata { get; } = new AgentMetadata
    {
        Name = "NotesSkill",
        Description = "Local-first note-taking skill with classification, summarization, and metadata extraction",
        Version = "1.0.0",
        SupportedTasks = new List<string>
        {
            "Add notes",
            "Search notes",
            "Classify notes (note/task/idea/reference)",
            "Extract tags and entities"
        },
        InputFormats = new List<string> { "text" },
        SupportedLanguages = new List<string> { "en" }
    };

    public async Task<bool> InitializeAsync(Dictionary<string, object>? configuration = null)
    {
        // Initialize the notes store
        string? basePath = null;

        if (configuration != null && configuration.TryGetValue("notesPath", out var path))
        {
            basePath = path.ToString();
        }

        _store = new JsonNotesStore(basePath);

        // Pre-load the cache
        await _store.GetAllAsync();

        return true;
    }

    public IAgent CreateAgent(ILLMClient? llmClient = null, Dictionary<string, object>? configuration = null)
    {
        if (_store == null)
        {
            throw new InvalidOperationException("Plugin not initialized. Call InitializeAsync first.");
        }

        return new NotesSkill(_store);
    }

    public Task DisposeAsync()
    {
        _store = null;
        return Task.CompletedTask;
    }
}
