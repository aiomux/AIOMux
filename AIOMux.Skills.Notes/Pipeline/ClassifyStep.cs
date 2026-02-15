using AIOMux.Skills.Notes.Models;

namespace AIOMux.Skills.Notes.Pipeline;

/// <summary>
/// Pipeline step to classify note type based on keywords and patterns.
/// </summary>
public static class ClassifyStep
{
    private static readonly string[] TaskKeywords = { "todo", "task", "need to", "must", "should", "remember to", "don't forget" };
    private static readonly string[] IdeaKeywords = { "idea", "maybe", "what if", "could", "brainstorm", "concept" };
    private static readonly string[] ReferenceKeywords = { "reference", "link", "source", "see", "check out", "read" };

    public static string Execute(string text)
    {
        var lowerText = text.ToLowerInvariant();

        // Check for task indicators
        if (TaskKeywords.Any(kw => lowerText.Contains(kw)))
        {
            return "task";
        }

        // Check for idea indicators
        if (IdeaKeywords.Any(kw => lowerText.Contains(kw)))
        {
            return "idea";
        }

        // Check for reference indicators
        if (ReferenceKeywords.Any(kw => lowerText.Contains(kw)) || lowerText.Contains("http"))
        {
            return "reference";
        }

        // Default to note
        return "note";
    }
}
