# NotesSkill Examples

## Basic Usage

### Adding Notes

```bash
# Simple note
dotnet run --project AIOMux.Local -- notes add --text "Met with the team to discuss Q1 roadmap"

# Task with hashtag
dotnet run --project AIOMux.Local -- notes add --text "TODO: Review security audit findings #security #urgent"

# Idea with mention
dotnet run --project AIOMux.Local -- notes add --text "Idea from @Alice: implement lazy loading for dashboard widgets"

# Reference with URL
dotnet run --project AIOMux.Local -- notes add --text "Check out this article on microservices https://example.com/microservices #architecture"
```

### Searching Notes

```bash
# Search by keyword
dotnet run --project AIOMux.Local -- notes search --q "security"

# Search for tasks (will match any text)
dotnet run --project AIOMux.Local -- notes search --q "TODO"

# List all notes
dotnet run --project AIOMux.Local -- notes list
```

## Expected Output

### Adding a Note

```
$ dotnet run --project AIOMux.Local -- notes add --text "Remember to review PR #123 #urgent"

[NotesSkill Event] InputReceived: {"step":"InputReceived","timestamp":"...","data":{...}}
[NotesSkill Event] Normalized: {"step":"Normalized","timestamp":"...","data":{...}}
[NotesSkill Event] Classified: {"step":"Classified","timestamp":"...","data":{"type":"task"}}
[NotesSkill Event] Summarized: {"step":"Summarized","timestamp":"...","data":{...}}
[NotesSkill Event] MetadataExtracted: {"step":"MetadataExtracted","timestamp":"...","data":{"tags":["urgent"],...}}
[NotesSkill Event] Persisted: {"step":"Persisted","timestamp":"...","data":{...}}

? Note added: Remember to review PR
  Type: task
  ID: 12345678-1234-1234-1234-123456789abc
  Tags: urgent
```

### Searching Notes

```
$ dotnet run --project AIOMux.Local -- notes search --q "review"

[NotesSkill Event] SearchRequested: {"step":"SearchRequested","timestamp":"...","data":{"query":"review"}}
[NotesSkill Event] SearchCompleted: {"step":"SearchCompleted","timestamp":"...","data":{"count":1}}

Found 1 note(s):

[task] Remember to review PR
  Remember to review PR #123
  Tags: urgent
  Created: 2024-01-15 14:30
  ID: 12345678-1234-1234-1234-123456789abc
```

### Listing All Notes

```
$ dotnet run --project AIOMux.Local -- notes list

[NotesSkill Event] ListRequested: {"step":"ListRequested","timestamp":"...","data":{}}
[NotesSkill Event] ListCompleted: {"step":"ListCompleted","timestamp":"...","data":{"count":3}}

Found 3 note(s):

[task] Remember to review PR
  Remember to review PR #123
  Tags: urgent
  Created: 2024-01-15 14:30
  ID: 12345678-1234-1234-1234-123456789abc

[idea] Implement lazy loading
  Idea from Alice: implement lazy loading for dashboard widgets.
  Entities: Alice
  Created: 2024-01-15 13:15
  ID: 87654321-4321-4321-4321-210987654321

[note] Met with the team
  Met with the team to discuss Q1 roadmap
  Created: 2024-01-15 10:00
  ID: abcdef12-3456-7890-abcd-ef1234567890
```

## Classification Examples

NotesSkill automatically classifies notes based on keywords:

### Task

Keywords: "todo", "task", "need to", "must", "should", "remember to", "don't forget"

```bash
dotnet run --project AIOMux.Local -- notes add --text "Need to update documentation"
# Type: task

dotnet run --project AIOMux.Local -- notes add --text "Don't forget to send the report"
# Type: task
```

### Idea

Keywords: "idea", "maybe", "what if", "could", "brainstorm", "concept"

```bash
dotnet run --project AIOMux.Local -- notes add --text "What if we use GraphQL instead of REST?"
# Type: idea

dotnet run --project AIOMux.Local -- notes add --text "Brainstorm: new onboarding flow"
# Type: idea
```

### Reference

Keywords: "reference", "link", "source", "see", "check out", "read", or contains "http"

```bash
dotnet run --project AIOMux.Local -- notes add --text "See the design doc for details"
# Type: reference

dotnet run --project AIOMux.Local -- notes add --text "Check https://docs.microsoft.com"
# Type: reference
```

### Note (Default)

Anything that doesn't match above patterns:

```bash
dotnet run --project AIOMux.Local -- notes add --text "Had a productive meeting today"
# Type: note
```

## Metadata Extraction Examples

### Hashtags ? Tags

```bash
dotnet run --project AIOMux.Local -- notes add --text "Performance optimization ideas #performance #caching #redis"
# Tags: performance, caching, redis
```

### @Mentions ? Entities

```bash
dotnet run --project AIOMux.Local -- notes add --text "Discussed with @Bob and @Alice"
# Entities: Bob, Alice
```

### URLs ? Entities

```bash
dotnet run --project AIOMux.Local -- notes add --text "Reference: https://github.com/aiomux/AIOMux"
# Entities: https://github.com/aiomux/AIOMux
```

### Proper Nouns ? Entities

```bash
dotnet run --project AIOMux.Local -- notes add --text "Meeting in Seattle with the Microsoft team"
# Entities: Meeting, Seattle, Microsoft
```

## Programmatic Usage

### Using NotesSkill as a Plugin

```csharp
using AIOMux.Core;
using AIOMux.Skills.Notes;
using AIOMux.Skills.Notes.Storage;

// Initialize
var store = new JsonNotesStore();
var skill = new NotesSkill(store);

// Create context
var context = new AgentContext
{
    UserInput = "Remember to review the code #urgent",
    Tools = new Dictionary<string, ITool>
    {
        { "NotesStore", store }
    }
};

// Execute
var result = await skill.ExecuteAsync(context);
Console.WriteLine(result);

// Get event log for replay
var events = skill.GetEventLog();
foreach (var evt in events)
{
    Console.WriteLine(evt);
}
```

### Using JsonNotesStore Directly

```csharp
using AIOMux.Skills.Notes.Storage;
using AIOMux.Skills.Notes.Models;

var store = new JsonNotesStore();

// Save
var note = new NoteRecord
{
    Title = "My Note",
    Summary = "This is a test note",
    Type = "note",
    Tags = new List<string> { "test" }
};
var id = await store.SaveAsync(note);

// Get
var retrieved = await store.GetAsync(id);

// Search
var results = await store.SearchAsync(new NoteQuery
{
    Tags = new List<string> { "test" }
});
```

## Storage Location

Notes are stored in:
- **Linux/macOS**: `~/.aiomux/notes.json`
- **Windows**: `C:\Users\<username>\.aiomux\notes.json`

The file format is a JSON array:

```json
[
  {
    "id": "12345678-1234-1234-1234-123456789abc",
    "title": "Remember to review PR",
    "summary": "Remember to review PR #123",
    "type": "task",
    "tags": ["urgent"],
    "entities": [],
    "createdUtc": "2024-01-15T14:30:00Z",
    "originalText": "Remember to review PR #123 #urgent",
    "source": "cli"
  }
]
```

## Testing

Run the test suite:

```bash
# Run all tests
dotnet test AIOMux.Skills.Notes.Tests

# Run specific test
dotnet test AIOMux.Skills.Notes.Tests --filter "FullyQualifiedName~PipelineTests"

# Run with verbose output
dotnet test AIOMux.Skills.Notes.Tests -v n
```

## Troubleshooting

### Permission Errors

If you see "NotesSkill may only use NotesStore tool":
- Ensure only NotesStore is in the context.Tools dictionary
- This is by design - NotesSkill enforces strict permissions

### File Not Found

If notes.json is not found:
- It will be created automatically on first save
- Check `~/.aiomux/` directory exists

### Concurrent Access

JsonNotesStore is thread-safe and uses:
- Semaphore for locking
- Atomic writes (temp file + replace)
- In-memory caching

Multiple processes can safely read, but writes should be serialized.
