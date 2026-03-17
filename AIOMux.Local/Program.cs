using AIOMux.Local.Commands;

namespace AIOMux.Local;

internal class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            if (args.Length == 0)
            {
                PrintUsage();
                Environment.Exit(0);
            }

            var command = args[0].ToLower();

            switch (command)
            {
                case "init":
                    InitCommand.Execute();
                    break;

                case "run":
                    var solutionPath = args.Length > 1 ? args[1] : null;
                    await RunCommand.ExecuteAsync(solutionPath);
                    break;

                case "notes":
                    await NotesCommand.ExecuteAsync(args);
                    break;

                case "runs":
                    await RunManagementCommand.ExecuteAsync(args);
                    break;

                case "demo":
                    if (args.Length > 1 && args[1].ToLower() == "notes")
                    {
                        await DemoNotesCommand.ExecuteAsync(args);
                    }
                    else
                    {
                        Console.Error.WriteLine("Unknown demo command. Try: aiomux demo notes");
                        PrintUsage();
                        Environment.Exit(1);
                    }
                    break;

                case "gauntlet":
                    await GauntletRagPoisonExfilCommand.ExecuteAsync(args);
                    break;

                case "-h":
                case "--help":
                case "help":
                    PrintUsage();
                    break;

                default:
                    Console.Error.WriteLine($"Unknown command: {command}");
                    PrintUsage();
                    Environment.Exit(1);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("AIOMux Local - Local workspace initialization and orchestration");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  aiomux init                    - Initialize a new workspace");
        Console.WriteLine("  aiomux run [solutionPath]      - Start the interactive REPL");
        Console.WriteLine("  aiomux notes <subcommand>      - Manage notes (add, search, list)");
        Console.WriteLine("  aiomux runs <subcommand>       - Manage runs (list, show, replay)");
        Console.WriteLine("  aiomux demo notes <subcommand> - Demo NotesSkill with replay");
        Console.WriteLine("  aiomux gauntlet rag-poison-exfil         - Run RAG poison exfiltration gauntlet demo");
        Console.WriteLine("  aiomux gauntlet fork-happy-path <runId>  - Fork a gauntlet run down the happy (unblocked) path");
        Console.WriteLine("  aiomux help                    - Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- init");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- run");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- run /path/to/workspace");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- notes add --text \"My note\"");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- notes search --q \"keyword\"");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- runs list");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- runs replay <runId>");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- demo notes add --text \"Demo note\" --replay");
        Console.WriteLine("  dotnet run --project AIOMux.Local -- gauntlet rag-poison-exfil");
    }
}
