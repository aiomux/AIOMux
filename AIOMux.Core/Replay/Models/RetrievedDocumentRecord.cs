namespace AIOMux.Core.Replay.Models;

/// <summary>
/// Represents a single document returned by retrieval.
/// </summary>
public class RetrievedDocumentRecord
{
    /// <summary>
    /// Identifier of the document.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Document title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Source location or provider.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// Rank returned by the retriever.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Excerpt used for context construction.
    /// </summary>
    public string Snippet { get; set; } = string.Empty;
}
