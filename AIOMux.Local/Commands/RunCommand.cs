using AIOMux.Local.Hosting;

namespace AIOMux.Local.Commands;

/// <summary>
/// Handles the 'run' command to start the interactive REPL.
/// </summary>
public class RunCommand
{
    /// <summary>
    /// Executes the REPL loop.
    /// </summary>
    public static async Task ExecuteAsync(string? solutionPath = null)
    {
        var basePath = solutionPath ?? Directory.GetCurrentDirectory();

        try
        {
            // Initialize the runtime
            var runner = new RunnerHost(basePath);
            await runner.InitializeAsync();

            // Start REPL loop
            await RunReplAsync(runner);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"? Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Runs the interactive REPL loop.
    /// </summary>
    private static async Task RunReplAsync(RunnerHost runner)
    {
        while (true)
        {
            Console.Write("aiomux> ");
            string? input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            var trimmed = input.Trim();

            // Handle built-in commands
            if (trimmed.Equals("exit", StringComparison.OrdinalIgnoreCase) || 
                trimmed.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("?? Goodbye!");
                break;
            }

            if (trimmed.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                PrintHelp();
                continue;
            }

            if (trimmed.Equals("agents", StringComparison.OrdinalIgnoreCase))
            {
                PrintAgents(runner);
                continue;
            }

            if (trimmed.Equals("config", StringComparison.OrdinalIgnoreCase))
            {
                PrintConfig(runner);
                continue;
            }

            // Execute as agent input
            try
            {
                var result = await runner.ExecuteAsync(trimmed);
                Console.WriteLine(result);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  exit/quit    - Exit the REPL");
        Console.WriteLine("  help         - Show this help message");
        Console.WriteLine("  agents       - List loaded agents");
        Console.WriteLine("  config       - Show current configuration");
        Console.WriteLine("  <input>      - Send input to the default agent");
        Console.WriteLine();
    }

    private static void PrintAgents(RunnerHost runner)
    {
        var manager = runner.GetAgentManager();
        var agents = manager.GetAllAgents();

        Console.WriteLine();
        Console.WriteLine("Loaded Agents:");
        if (agents.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var agent in agents)
            {
                Console.WriteLine($"  • {agent.Name}");
            }
        }
        Console.WriteLine();
    }

    private static void PrintConfig(RunnerHost runner)
    {
        var config = runner.GetConfig();

        Console.WriteLine();
        Console.WriteLine("Current Configuration:");
        Console.WriteLine($"  DefaultAgentName: {config.DefaultAgentName}");
        Console.WriteLine($"  Model Provider: {config.Model.Provider}");
        Console.WriteLine($"  Model Name: {config.Model.ModelName}");
        if (!string.IsNullOrEmpty(config.Model.BaseUrl))
        {
            Console.WriteLine($"  Base URL: {config.Model.BaseUrl}");
        }
        Console.WriteLine($"  SkillsPath: {config.SkillsPath}");
        Console.WriteLine();
    }
}
