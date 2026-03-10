namespace AIOMux.Core.Replay.Models;

public class RetrievedDocumentRecord
{
    public string DocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int Rank { get; set; }
    public string Snippet { get; set; } = string.Empty;
}
