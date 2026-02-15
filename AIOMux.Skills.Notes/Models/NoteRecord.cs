namespace AIOMux.Skills.Notes.Models;

/// <summary>
/// Persisted note record with metadata.
/// </summary>
public class NoteRecord
{
    /// <summary>
    /// Unique identifier for the note.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Extracted or generated title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Summarized content.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Classified note type (note, task, idea, reference).
    /// </summary>
    public string Type { get; set; } = "note";

    /// <summary>
    /// Extracted tags.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Extracted entities (people, places, concepts).
    /// </summary>
    public List<string> Entities { get; set; } = new();

    /// <summary>
    /// Original creation timestamp.
    /// </summary>
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original text content.
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// Source identifier.
    /// </summary>
    public string? Source { get; set; }
}
