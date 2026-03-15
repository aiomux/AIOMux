using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Skills.Notes.Models;
using AIOMux.Skills.Notes.Pipeline;
using AIOMux.Skills.Notes.Storage;
using System.Text.Json;

namespace AIOMux.Skills.Notes;

/// <summary>
/// NotesSkill agent that processes notes through a pipeline and persists them.
/// Pipeline: NormalizeInput -> Classify -> Summarize -> ExtractMetadata -> Persist
/// Only allowed to use NotesStore tool.
/// </summary>
public class NotesSkill : IAgent
{
    private readonly INotesStore _store;

    public string Name => "NotesSkill";

    public NotesSkill(INotesStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<string> ExecuteAsync(AgentContext context)
    {
        try
        {
            // Enforce permission: only NotesStore tool allowed
            if (context.Tools.Count > 1 || (context.Tools.Count == 1 && !context.Tools.ContainsKey("NotesStore")))
            {
                throw new UnauthorizedAccessException("NotesSkill may only use NotesStore tool");
            }

            // Parse input - could be add command or search command
            var input = context.UserInput.Trim();

            // Emit event: input received
            EmitEvent("InputReceived", new { input });

            // Check for search command
            if (input.StartsWith("search", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleSearchAsync(input);
            }

            // Check for list command
            if (input.Equals("list", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return await HandleListAsync();
            }

            // Default: add note
            return await HandleAddNoteAsync(input);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> HandleAddNoteAsync(string text)
    {
        // Step 1: Normalize
        var noteInput = new NoteInput
        {
            Text = text,
            Source = "cli",
            CreatedUtc = DateTime.UtcNow
        };

        noteInput = NormalizeStep.Execute(noteInput);
        EmitEvent("Normalized", new { text = noteInput.Text });

        // Step 2: Classify
        var noteType = ClassifyStep.Execute(noteInput.Text);
        EmitEvent("Classified", new { type = noteType });

        // Step 3: Summarize
        var (title, summary) = SummarizeStep.Execute(noteInput.Text);
        EmitEvent("Summarized", new { title, summary });

        // Step 4: Extract Metadata
        var (tags, entities) = ExtractMetadataStep.Execute(noteInput.Text);
        EmitEvent("MetadataExtracted", new { tags, entities });

        // Step 5: Persist
        var record = new NoteRecord
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Summary = summary,
            Type = noteType,
            Tags = tags,
            Entities = entities,
            CreatedUtc = noteInput.CreatedUtc,
            OriginalText = noteInput.Text,
            Source = noteInput.Source
        };

        var savedId = await _store.SaveAsync(record);
        EmitEvent("Persisted", new { id = savedId });

        return FormatNoteAdded(record);
    }

    private async Task<string> HandleSearchAsync(string input)
    {
        // Parse search query
        var queryText = input.Substring("search".Length).Trim();

        var query = new NoteQuery
        {
            Q = string.IsNullOrWhiteSpace(queryText) ? null : queryText,
            MaxResults = 10
        };

        EmitEvent("SearchRequested", new { query = queryText });

        var results = await _store.SearchAsync(query);

        EmitEvent("SearchCompleted", new { count = results.Count });

        return FormatSearchResults(results);
    }

    private async Task<string> HandleListAsync()
    {
        EmitEvent("ListRequested", new { });

        var results = await _store.GetAllAsync();

        EmitEvent("ListCompleted", new { count = results.Count });

        return FormatSearchResults(results);
    }

    private void EmitEvent(string step, object data)
    {
        var eventData = new
        {
            step,
            timestamp = DateTime.UtcNow,
            data
        };

        var json = JsonSerializer.Serialize(eventData);
        Console.WriteLine($"[NotesSkill Event] {step}: {json}");
    }

    private string FormatNoteAdded(NoteRecord note)
    {
        var result = $"? Note added: {note.Title}\n";
        result += $"  Type: {note.Type}\n";
        result += $"  ID: {note.Id}\n";

        if (note.Tags.Count > 0)
        {
            result += $"  Tags: {string.Join(", ", note.Tags)}\n";
        }

        if (note.Entities.Count > 0)
        {
            result += $"  Entities: {string.Join(", ", note.Entities)}\n";
        }

        return result;
    }

    private string FormatSearchResults(List<NoteRecord> results)
    {
        if (results.Count == 0)
        {
            return "No notes found.";
        }

        var output = $"Found {results.Count} note(s):\n\n";

        foreach (var note in results)
        {
            output += $"[{note.Type}] {note.Title}\n";
            output += $"  {note.Summary}\n";
            if (note.Tags.Count > 0)
            {
                output += $"  Tags: {string.Join(", ", note.Tags)}\n";
            }
            output += $"  Created: {note.CreatedUtc:yyyy-MM-dd HH:mm}\n";
            output += $"  ID: {note.Id}\n";
            output += "\n";
        }

        return output;
    }
}
