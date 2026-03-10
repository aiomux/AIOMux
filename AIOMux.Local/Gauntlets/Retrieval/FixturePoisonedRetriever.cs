using AIOMux.Core.Replay.Models;

namespace AIOMux.Local.Gauntlets.Retrieval;

public class FixturePoisonedRetriever
{
    private const string DefaultFixtureRelativePath = "Gauntlets/Fixtures/rag-poisoned-doc.txt";
    private readonly PoisonedFixtureDocument _fixture;

    public FixturePoisonedRetriever(string? fixturePath = null)
    {
        var path = fixturePath ?? ResolveFixturePath();
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Poisoned fixture file not found at '{path}'.", path);
        }

        _fixture = ParseFixture(File.ReadAllText(path));
    }

    public Task<RetrievalCompletedEvent.RetrievalCompletedPayload> RetrieveAsync(string query, int topK, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var payload = new RetrievalCompletedEvent.RetrievalCompletedPayload
        {
            RetrieverName = nameof(FixturePoisonedRetriever),
            Query = query,
            TopK = topK,
            Documents = new List<RetrievedDocumentRecord>
            {
                new()
                {
                    DocumentId = _fixture.DocumentId,
                    Title = _fixture.Title,
                    Source = _fixture.Source,
                    Rank = 1,
                    Snippet = _fixture.Snippet
                }
            },
            CombinedContext = _fixture.CombinedContext
        };

        return Task.FromResult(payload);
    }

    private static PoisonedFixtureDocument ParseFixture(string content)
    {
        var separator = "\n---\n";
        var normalized = content.Replace("\r\n", "\n");
        var separatorIndex = normalized.IndexOf(separator, StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            throw new InvalidOperationException("Invalid poisoned fixture format. Missing metadata separator '---'.");
        }

        var header = normalized[..separatorIndex];
        var combinedContext = normalized[(separatorIndex + separator.Length)..].Trim();

        string ReadHeaderValue(string key)
        {
            var prefix = key + ":";
            var line = header
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            if (line == null)
            {
                throw new InvalidOperationException($"Invalid poisoned fixture format. Missing '{key}' metadata.");
            }

            return line[prefix.Length..].Trim();
        }

        return new PoisonedFixtureDocument
        {
            DocumentId = ReadHeaderValue("DocumentId"),
            Title = ReadHeaderValue("Title"),
            Source = ReadHeaderValue("Source"),
            Snippet = ReadHeaderValue("Snippet"),
            CombinedContext = combinedContext
        };
    }

    private static string ResolveFixturePath()
    {
        return Path.Combine(AppContext.BaseDirectory, DefaultFixtureRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private sealed class PoisonedFixtureDocument
    {
        public string DocumentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public string Snippet { get; set; } = string.Empty;
        public string CombinedContext { get; set; } = string.Empty;
    }
}
