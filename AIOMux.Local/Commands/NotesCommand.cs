using AIOMux.Skills.Notes;
using AIOMux.Skills.Notes.Storage;
using AIOMux.Core;

namespace AIOMux.Local.Commands;

/// <summary>
/// Handles the 'notes' command for direct note operations.
/// </summary>
public class NotesCommand
{
    /// <summary>
    /// Execute notes command.
    /// Usage:
    ///   aiomux notes add --text "..."
    ///   aiomux notes search --q "..."
    ///   aiomux notes list
    /// </summary>
    public static async Task ExecuteAsync(string[] args)
    {
        if (args.Length < 2)
        {
            PrintUsage();
            return;
        }

        var subCommand = args[1].ToLowerInvariant();

        try
        {
            // Initialize the notes store
            var store = new JsonNotesStore();
            var skill = new NotesSkill(store);

            // Create agent context
            var context = new AgentContext
            {
                Tools = new Dictionary<string, Core.Interfaces.ITool>
                {
                    { "NotesStore", store }
                }
            };

            switch (subCommand)
            {
                case "add":
                    await HandleAddAsync(skill, context, args);
                    break;

                case "search":
                    await HandleSearchAsync(skill, context, args);
                    break;

                case "list":
                    await HandleListAsync(skill, context);
                    break;

                default:
                    Console.Error.WriteLine($"Unknown notes subcommand: {subCommand}");
                    PrintUsage();
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static async Task HandleAddAsync(NotesSkill skill, AgentContext context, string[] args)
    {
        // Parse --text argument
        string? text = null;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--text" && i + 1 < args.Length)
            {
                text = args[i + 1];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Console.Error.WriteLine("Error: --text argument is required");
            PrintUsage();
            return;
        }

        context.UserInput = text;
        var result = await skill.ExecuteAsync(context);
        Console.WriteLine(result);
    }

    private static async Task HandleSearchAsync(NotesSkill skill, AgentContext context, string[] args)
    {
        // Parse --q argument
        string? query = null;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--q" && i + 1 < args.Length)
            {
                query = args[i + 1];
                break;
            }
        }

        context.UserInput = string.IsNullOrWhiteSpace(query) ? "search" : $"search {query}";
        var result = await skill.ExecuteAsync(context);
        Console.WriteLine(result);
    }

    private static async Task HandleListAsync(NotesSkill skill, AgentContext context)
    {
        context.UserInput = "list";
        var result = await skill.ExecuteAsync(context);
        Console.WriteLine(result);
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Notes Command Usage:");
        Console.WriteLine();
        Console.WriteLine("  aiomux notes add --text \"<note text>\"");
        Console.WriteLine("  aiomux notes search --q \"<search query>\"");
        Console.WriteLine("  aiomux notes list");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  aiomux notes add --text \"Remember to review PR #123\"");
        Console.WriteLine("  aiomux notes add --text \"Idea: implement caching layer #performance\"");
        Console.WriteLine("  aiomux notes search --q \"caching\"");
        Console.WriteLine("  aiomux notes list");
    }
}
