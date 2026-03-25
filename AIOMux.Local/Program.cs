namespace AIOMux.Local;

internal static class Program
{
    private const string SolutionFileName = "solution.json";

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].Trim().ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();

        return command switch
        {
            "run" => await RunAsync(commandArgs),
            "validate" => Validate(commandArgs),
            "replay" => Replay(commandArgs),
            "fork" => Fork(commandArgs),
            "help" or "--help" or "-h" => PrintHelpAndExit(),
            _ => UnknownCommand(command)
        };
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Missing solution path.");
            PrintUsage();
            return 1;
        }

        var solutionJsonPath = ResolveSolutionJsonPath(args[0]);
        var input = args.Length > 1 ? string.Join(' ', args.Skip(1)) : string.Empty;

        Console.WriteLine("Path: SolutionLoader -> SolutionRunner -> ExecutionRuntime");

        var runner = new SolutionRunner();

        var summary = await runner.RunAsync(solutionJsonPath, input);
        PrintRunSummary(summary);

        return summary.Success ? 0 : 1;
    }

    private static int Validate(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Missing solution path.");
            PrintUsage();
            return 1;
        }

        try
        {
            var solutionJsonPath = ResolveSolutionJsonPath(args[0]);
            var loader = new SolutionLoader(solutionJsonPath);
            var solution = loader.Load();
            loader.ValidateReferences(solution);

            Console.WriteLine($"Valid solution: {solution.Name}");
            Console.WriteLine($"  Entry: {solution.Entry}");
            Console.WriteLine($"  WorkingDirectory: {solution.WorkingDirectory}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Validation failed: {ex.Message}");
            return 1;
        }
    }

    private static int Replay(string[] args)
    {
        _ = args;
        Console.Error.WriteLine("'replay' is reserved for a future command. Use 'run' for now.");
        return 2;
    }

    private static int Fork(string[] args)
    {
        _ = args;
        Console.Error.WriteLine("'fork' is reserved for a future command. Use 'run' for now.");
        return 2;
    }

    private static string ResolveSolutionJsonPath(string pathOrFolder)
    {
        var fullPath = Path.GetFullPath(pathOrFolder);

        if (Directory.Exists(fullPath))
        {
            var solutionPath = Path.Combine(fullPath, SolutionFileName);
            if (!File.Exists(solutionPath))
                throw new FileNotFoundException($"Could not find '{SolutionFileName}' in '{fullPath}'.");

            return solutionPath;
        }

        if (File.Exists(fullPath))
            return fullPath;

        throw new FileNotFoundException($"Could not find solution path '{pathOrFolder}'.");
    }

    private static void PrintRunSummary(SolutionExecutionSummary summary)
    {
        var status = summary.Success ? "Success" : "Failed";
        Console.WriteLine($"{status}: {summary.SolutionName} [{summary.ExecutedSteps}/{summary.StepCount}] {summary.DurationMs:F0}ms");

        if (!string.IsNullOrWhiteSpace(summary.Error))
            Console.WriteLine($"Error: {summary.Error}");

        if (summary.Success && !string.IsNullOrWhiteSpace(summary.Output))
            Console.WriteLine($"Output: {TruncateSingleLine(summary.Output, 180)}");
    }

    private static string TruncateSingleLine(string value, int maxLength)
    {
        var singleLine = value.Replace("\r", " ").Replace("\n", " ").Trim();
        if (singleLine.Length <= maxLength)
            return singleLine;

        return singleLine[..maxLength] + "...";
    }

    private static int PrintHelpAndExit()
    {
        PrintUsage();
        return 0;
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("AIOMux.Local CLI");
        Console.WriteLine();
        Console.WriteLine("run <solution-path> [input]");
        Console.WriteLine("  Executes solution.json via SolutionLoader -> SolutionRunner -> ExecutionRuntime");
        Console.WriteLine();
        Console.WriteLine("validate <solution-path>");
        Console.WriteLine("  Validates solution.json and referenced files");
        Console.WriteLine();
        Console.WriteLine("replay <...>");
        Console.WriteLine("fork <...>");
        Console.WriteLine("  Reserved seams for future replay/fork workflows");
    }
}
