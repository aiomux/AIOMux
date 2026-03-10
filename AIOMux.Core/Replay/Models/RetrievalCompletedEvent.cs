using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

public class RetrievalCompletedEvent : RuntimeEvent
{
    public RetrievalCompletedEvent()
    {
        Type = "RetrievalCompleted";
    }

    public class RetrievalCompletedPayload
    {
        public string RetrieverName { get; set; } = string.Empty;
        public string Query { get; set; } = string.Empty;
        public int TopK { get; set; }
        public List<RetrievedDocumentRecord> Documents { get; set; } = new();
        public string CombinedContext { get; set; } = string.Empty;
    }
}
