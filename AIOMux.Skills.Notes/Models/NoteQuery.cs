namespace AIOMux.Skills.Notes.Models;

/// <summary>
/// Query parameters for searching notes.
/// </summary>
public class NoteQuery
{
    /// <summary>
    /// Text search query (searches title, summary, and original text).
    /// </summary>
    public string? Q { get; set; }

    /// <summary>
    /// Filter by specific tags.
    /// </summary>
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Filter by note type (note, task, idea, reference).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    public int MaxResults { get; set; } = 50;
}
