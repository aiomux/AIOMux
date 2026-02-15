using AIOMux.Skills.Notes.Models;

namespace AIOMux.Skills.Notes.Pipeline;

/// <summary>
/// Pipeline step to normalize input text.
/// </summary>
public static class NormalizeStep
{
    public static NoteInput Execute(NoteInput input)
    {
        // Normalize whitespace and trim
        input.Text = string.Join(" ", input.Text.Split(new[] { ' ', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries));

        return input;
    }
}
