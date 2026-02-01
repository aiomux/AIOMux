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
        Console.WriteLine("  aiomux help                    - Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run --project src/AIOMux.Local -- init");
        Console.WriteLine("  dotnet run --project src/AIOMux.Local -- run");
        Console.WriteLine("  dotnet run --project src/AIOMux.Local -- run /path/to/workspace");
    }
}
