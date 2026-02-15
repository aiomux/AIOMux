using System.Text.RegularExpressions;

namespace AIOMux.Skills.Notes.Pipeline;

/// <summary>
/// Pipeline step to extract metadata: tags, entities, and dates.
/// </summary>
public static class ExtractMetadataStep
{
    private static readonly Regex HashtagRegex = new(@"#(\w+)", RegexOptions.Compiled);
    private static readonly Regex MentionRegex = new(@"@(\w+)", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"https?://[^\s]+", RegexOptions.Compiled);

    public static (List<string> tags, List<string> entities) Execute(string text)
    {
        var tags = new List<string>();
        var entities = new List<string>();

        // Extract hashtags as tags
        var hashtagMatches = HashtagRegex.Matches(text);
        foreach (Match match in hashtagMatches)
        {
            tags.Add(match.Groups[1].Value.ToLowerInvariant());
        }

        // Extract @mentions as entities
        var mentionMatches = MentionRegex.Matches(text);
        foreach (Match match in mentionMatches)
        {
            entities.Add(match.Groups[1].Value);
        }

        // Extract URLs as entities
        var urlMatches = UrlRegex.Matches(text);
        foreach (Match match in urlMatches)
        {
            entities.Add(match.Value);
        }

        // Extract capitalized words as potential entities (simple NER)
        var words = text.Split(new[] { ' ', '\t', '\r', '\n', '.', ',', '!', '?' },
            StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (word.Length > 2 && char.IsUpper(word[0]) && word.Skip(1).All(char.IsLower))
            {
                // Likely a proper noun
                if (!entities.Contains(word))
                {
                    entities.Add(word);
                }
            }
        }

        return (tags.Distinct().ToList(), entities.Distinct().Take(10).ToList());
    }
}
