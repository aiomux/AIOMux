using AIOMux.Core.Models;

namespace AIOMux.Core.Replay.Models;

/// <summary>
/// Runtime event emitted when retrieval completes.
/// </summary>
public class RetrievalCompletedEvent : RuntimeEvent
{
    public RetrievalCompletedEvent()
    {
        Type = "RetrievalCompleted";
    }

    /// <summary>
    /// Payload for a retrieval-completed event.
    /// </summary>
    public class RetrievalCompletedPayload
    {
        /// <summary>
        /// Name of the retriever used.
        /// </summary>
        public string RetrieverName { get; set; } = string.Empty;

        /// <summary>
        /// Query submitted to the retriever.
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Maximum number of documents requested.
        /// </summary>
        public int TopK { get; set; }

        /// <summary>
        /// Retrieved documents.
        /// </summary>
        public List<RetrievedDocumentRecord> Documents { get; set; } = new();

        /// <summary>
        /// Combined context assembled from retrieved documents.
        /// </summary>
        public string CombinedContext { get; set; } = string.Empty;
    }
}
