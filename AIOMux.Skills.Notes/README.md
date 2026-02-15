# AIOMux.Skills.Notes

A local-first note-taking skill for AIOMux with intelligent classification, summarization, and metadata extraction.

## Overview

NotesSkill is a pipeline-based agent that processes notes through multiple stages:

1. **NormalizeInput** - Cleans and normalizes text
2. **Classify** - Categorizes as note/task/idea/reference
3. **Summarize** - Extracts title and creates summary
4. **ExtractMetadata** - Identifies tags (#hashtags), entities (@mentions, URLs, proper nouns)
5. **Persist** - Saves to JSON storage

## Features

- **Local-first**: All data stored in `~/.aiomux/notes.json`
- **No network access**: Pure local processing
- **Single tool restriction**: Only uses NotesStore (enforced by permission system)
- **Event replay**: All pipeline steps emit events for debugging and replay
- **Thread-safe**: Atomic writes with in-memory caching
- **Searchable**: Full-text search with tag and type filtering

## Installation

NotesSkill is part of the AIOMux solution. Build it with:

```bash
dotnet build AIOMux.Skills.Notes
```

## Usage

### As a Plugin

Copy the built DLL to your AIOMux workspace's `skills/` directory:

```bash
cp bin/Debug/net10.0/AIOMux.Skills.Notes.dll /path/to/workspace/skills/
```

Then run:

```bash
dotnet run --project AIOMux.Local -- run
```

### CLI Commands

```bash
# Add a note
dotnet run --project AIOMux.Local -- notes add --text "Remember to review PR #123"

# Add a task
dotnet run --project AIOMux.Local -- notes add --text "TODO: implement caching layer #performance"

# Add an idea
dotnet run --project AIOMux.Local -- notes add --text "Idea: what if we use Redis for session storage?"

# Search notes
dotnet run --project AIOMux.Local -- notes search --q "caching"

# List all notes
dotnet run --project AIOMux.Local -- notes list
```

## DTOs

### NoteInput

```csharp
{
    "text": "The note content",
    "source": "cli|api|repl",
    "createdUtc": "2024-01-01T00:00:00Z"
}
```

### NoteRecord

```csharp
{
    "id": "guid",
    "title": "Extracted title",
    "summary": "Summary of content",
    "type": "note|task|idea|reference",
    "tags": ["tag1", "tag2"],
    "entities": ["Person", "https://url.com"],
    "createdUtc": "2024-01-01T00:00:00Z",
    "originalText": "Full original text",
    "source": "cli"
}
```

### NoteQuery

```csharp
{
    "q": "search text",
    "tags": ["tag1"],
    "type": "task",
    "maxResults": 50
}
```

## Storage

### JsonNotesStore

- **Location**: `~/.aiomux/notes.json` (or custom path)
- **Format**: JSON array of NoteRecord objects
- **Atomic writes**: Uses temp file + replace pattern
- **Caching**: In-memory cache with file monitoring
- **Thread-safe**: Semaphore-based locking

### INotesStore Interface

```csharp
Task<string> SaveAsync(NoteRecord note);
Task<NoteRecord?> GetAsync(string id);
Task<List<NoteRecord>> SearchAsync(NoteQuery query);
Task<List<NoteRecord>> GetAllAsync();
```

## Pipeline Steps

### 1. NormalizeStep

- Removes extra whitespace
- Normalizes newlines and tabs
- Trims leading/trailing space

### 2. ClassifyStep

Keyword-based classification:
- **task**: "todo", "need to", "must", "remember to"
- **idea**: "idea", "maybe", "what if", "brainstorm"
- **reference**: "reference", "link", "http"
- **note**: default

### 3. SummarizeStep

- Extracts title (first sentence or 60 chars)
- Creates summary (up to 200 chars at sentence boundary)

### 4. ExtractMetadataStep

- **Tags**: #hashtags
- **Entities**: @mentions, URLs, proper nouns (capitalized words)
- Limits entities to top 10

## Permission System

NotesSkill enforces strict permissions:

```csharp
// ? Allowed
context.Tools = new Dictionary<string, ITool>
{
    { "NotesStore", store }
};

// ? Denied - throws UnauthorizedAccessException
context.Tools = new Dictionary<string, ITool>
{
    { "NotesStore", store },
    { "FileSystem", fileSystem }  // Not allowed!
};
```

## Events

Each pipeline step emits an event:

```json
{
    "step": "Normalized",
    "timestamp": "2024-01-01T00:00:00Z",
    "data": { "text": "normalized text" }
}
```

Events are logged to console and available via `GetEventLog()` for testing/replay.

## Testing

Run tests:

```bash
dotnet test AIOMux.Skills.Notes.Tests
```

Tests cover:
- ? Deterministic pipeline output
- ? Permission violations
- ? JSON persistence and search
- ? Concurrent access (thread safety)
- ? Event logging
- ? All pipeline steps

## Architecture

### SOLID Principles

- **SRP**: Each pipeline step has one responsibility
- **OCP**: Pipeline is extensible (add new steps)
- **LSP**: Implements IAgent and ITool interfaces
- **ISP**: Minimal interfaces (INotesStore)
- **DIP**: Depends on abstractions (INotesStore, ITool)

### Project Structure

```
AIOMux.Skills.Notes/
??? Models/
?   ??? NoteInput.cs
?   ??? NoteRecord.cs
?   ??? NoteQuery.cs
??? Pipeline/
?   ??? NormalizeStep.cs
?   ??? ClassifyStep.cs
?   ??? SummarizeStep.cs
?   ??? ExtractMetadataStep.cs
??? Storage/
?   ??? INotesStore.cs
?   ??? JsonNotesStore.cs
??? NotesSkill.cs
??? NotesSkillPlugin.cs
```

## Limitations (MVP)

- No LLM integration (pure algorithmic processing)
- Simple keyword-based classification
- Basic summarization (truncation with sentence boundaries)
- No SQLite or database support
- No network access
- English-only entity extraction

## Future Enhancements

- LLM-powered summarization and classification
- Multi-language support
- Rich metadata (dates, locations, sentiment)
- Note relationships and linking
- Export formats (Markdown, JSON, CSV)
- Encryption at rest

## License

Part of AIOMux, licensed under MIT License.
