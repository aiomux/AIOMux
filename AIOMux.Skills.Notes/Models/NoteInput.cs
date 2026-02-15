namespace AIOMux.Skills.Notes.Models;

/// <summary>
/// Input DTO for creating a new note.
/// </summary>
public class NoteInput
{
    /// <summary>
    /// The raw text content of the note.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional source identifier (e.g., "cli", "api", "repl").
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Timestamp when the note was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
