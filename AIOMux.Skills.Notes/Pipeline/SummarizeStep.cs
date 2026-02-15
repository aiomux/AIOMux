namespace AIOMux.Skills.Notes.Pipeline;

/// <summary>
/// Pipeline step to create a summary of the note.
/// For MVP, uses simple truncation with smart sentence boundary detection.
/// </summary>
public static class SummarizeStep
{
    private const int MaxSummaryLength = 200;

    public static (string title, string summary) Execute(string text)
    {
        // Extract title from first sentence or first N characters
        var title = ExtractTitle(text);
        var summary = ExtractSummary(text);

        return (title, summary);
    }

    private static string ExtractTitle(string text)
    {
        // Use first sentence or first 60 chars as title
        var firstSentenceEnd = text.IndexOfAny(new[] { '.', '!', '?' });
        if (firstSentenceEnd > 0 && firstSentenceEnd < 60)
        {
            return text.Substring(0, firstSentenceEnd).Trim();
        }

        return text.Length > 60 ? text.Substring(0, 57).Trim() + "..." : text.Trim();
    }

    private static string ExtractSummary(string text)
    {
        if (text.Length <= MaxSummaryLength)
        {
            return text;
        }

        // Find last sentence boundary before max length
        var truncated = text.Substring(0, MaxSummaryLength);
        var lastSentence = truncated.LastIndexOfAny(new[] { '.', '!', '?' });

        if (lastSentence > MaxSummaryLength / 2)
        {
            return text.Substring(0, lastSentence + 1).Trim();
        }

        // No good sentence boundary, just truncate with ellipsis
        return truncated.Trim() + "...";
    }
}
