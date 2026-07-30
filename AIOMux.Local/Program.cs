using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIOMux.Local;

internal static class Program
{
    private const string SolutionFileName = "solution.json";
    private static ILoggerFactory? _debugLoggerFactory;
    private static string? _reportRootDirectory;

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
            "serve" => await ServeAsync(commandArgs),
            "validate" => Validate(commandArgs),
            "replay" => await ReplayAsync(commandArgs),
            "fork" => await ForkAsync(commandArgs),
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
        _reportRootDirectory = Path.GetDirectoryName(solutionJsonPath);

        var commandInput = ParseCommandInput(args.Skip(1).ToArray());
        var input = string.Join(' ', new[] { commandInput.Verb }.Concat(commandInput.Parameters).Where(v => !string.IsNullOrWhiteSpace(v)));
        var additionalInputs = new Dictionary<string, object?>
        {
            ["command_verb"] = commandInput.Verb,
            ["command_parameters"] = commandInput.Parameters
        };

        try
        {
            var validator = new SolutionValidator();
            await validator.ValidateAsync(solutionJsonPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Validation failed: {ex.Message}");
            return 1;
        }

        Console.WriteLine("Path: SolutionLoader -> SolutionRunner -> ExecutionRuntime");

        var runner = CreateRunner();

        var summary = await runner.RunAsync(solutionJsonPath, input, additionalInputs);
        PrintRunSummary(summary);

        if (!summary.Success && !string.IsNullOrWhiteSpace(summary.Output))
            TryPrintStructuredError(summary.Output);

        return summary.Success ? 0 : 1;
    }

    private static async Task<int> ServeAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Missing solution path.");
            PrintUsage();
            return 1;
        }

        var solutionJsonPath = ResolveSolutionJsonPath(args[0]);

        try
        {
            var validator = new SolutionValidator();
            await validator.ValidateAsync(solutionJsonPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Validation failed: {ex.Message}");
            return 1;
        }

        var runner = CreateRunner();

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        Console.WriteLine("Serve mode active. Type a line to trigger execution. Press Ctrl+C to stop.");

        try
        {
            await runner.ServeAsync(solutionJsonPath, cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Serve mode stopped.");
        }

        return 0;
    }

    private static CommandInput ParseCommandInput(string[] args)
    {
        if (args.Length == 0)
        {
            return new CommandInput
            {
                Verb = string.Empty,
                Parameters = []
            };
        }

        return new CommandInput
        {
            Verb = args[0],
            Parameters = args.Skip(1).ToArray()
        };
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

    private static async Task<int> ReplayAsync(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Missing required arguments for replay.");
            PrintUsage();
            return 1;
        }

        var solutionJsonPath = ResolveSolutionJsonPath(args[0]);
        _reportRootDirectory = Path.GetDirectoryName(solutionJsonPath);
        var sourceRunId = args[1];
        var input = args.Length > 2 ? string.Join(' ', args.Skip(2)) : null;

        try
        {
            var validator = new SolutionValidator();
            await validator.ValidateAsync(solutionJsonPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Validation failed: {ex.Message}");
            return 1;
        }

        var runner = CreateRunner();
        var summary = await runner.ReplayAsync(solutionJsonPath, sourceRunId, input);
        PrintRunSummary(summary);

        if (!summary.Success && !string.IsNullOrWhiteSpace(summary.Output))
            TryPrintStructuredError(summary.Output);

        return summary.Success ? 0 : 1;
    }

    private static void TryPrintStructuredError(string output)
    {
        try
        {
            using var doc = JsonDocument.Parse(output);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("error", out var errorElement))
            {
                var detailedError = errorElement.GetString();
                if (!string.IsNullOrWhiteSpace(detailedError))
                    Console.WriteLine($"Detailed error: {detailedError}");
            }
        }
        catch
        {
            // Ignore parse errors - output can be plain text.
        }
    }

    private static async Task<int> ForkAsync(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("Missing required arguments for fork.");
            PrintUsage();
            return 1;
        }

        var solutionJsonPath = ResolveSolutionJsonPath(args[0]);
        _reportRootDirectory = Path.GetDirectoryName(solutionJsonPath);
        var sourceRunId = args[1];
        if (!int.TryParse(args[2], out var forkStepIndex))
        {
            Console.Error.WriteLine("Invalid fork step index. Expected an integer value.");
            return 1;
        }

        var input = args.Length > 3 ? string.Join(' ', args.Skip(3)) : null;

        try
        {
            var validator = new SolutionValidator();
            await validator.ValidateAsync(solutionJsonPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Validation failed: {ex.Message}");
            return 1;
        }

        var runner = CreateRunner();
        var summary = await runner.ForkAsync(solutionJsonPath, sourceRunId, forkStepIndex, input);
        PrintRunSummary(summary);

        if (!summary.Success && !string.IsNullOrWhiteSpace(summary.Output))
            TryPrintStructuredError(summary.Output);

        return summary.Success ? 0 : 1;
    }

    private static SolutionRunner CreateRunner()
    {
        var debugLoggingEnabled = string.Equals(
            Environment.GetEnvironmentVariable("AIOMUX_DEBUG_LOGGING"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!debugLoggingEnabled)
            return new SolutionRunner();

        _debugLoggerFactory ??= LoggerFactory.Create(builder =>
        {
            builder
                .SetMinimumLevel(LogLevel.Debug)
                .AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                })
                .AddDebug();
        });

        return new SolutionRunner(
            logger: _debugLoggerFactory.CreateLogger<SolutionRunner>(),
            loggerFactory: _debugLoggerFactory);
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

        if (!string.IsNullOrWhiteSpace(summary.Output))
        {
            Console.WriteLine($"Output: {TruncateSingleLine(summary.Output, 180)}");

            var reportPath = WriteReportFile(summary);
            if (!string.IsNullOrWhiteSpace(reportPath))
                Console.WriteLine($"Report: {reportPath}");
        }
    }

    private static string? WriteReportFile(SolutionExecutionSummary summary)
    {
        if (string.IsNullOrWhiteSpace(summary.Output))
            return null;

        var runId = string.IsNullOrWhiteSpace(summary.RunId)
            ? DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
            : summary.RunId;

        var rootDirectory = string.IsNullOrWhiteSpace(_reportRootDirectory)
            ? Directory.GetCurrentDirectory()
            : _reportRootDirectory;

        var reportsDirectory = Path.Combine(rootDirectory, "reports");
        Directory.CreateDirectory(reportsDirectory);

        var outputText = TryFormatJson(summary.Output, out var formattedJson)
            ? formattedJson
            : summary.Output;

        var extension = TryFormatJson(summary.Output, out _) ? "json" : "txt";
        var fileName = $"aiomux-report-{runId}.{extension}";
        var fullPath = Path.Combine(reportsDirectory, fileName);
        File.WriteAllText(fullPath, outputText);
        return fullPath;
    }

    private static bool TryFormatJson(string value, out string formatted)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            formatted = JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            return true;
        }
        catch
        {
            formatted = value;
            return false;
        }
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
        Console.WriteLine("serve <solution-path>");
        Console.WriteLine("  Starts serve mode: loads solution and runs connectors until Ctrl+C");
        Console.WriteLine();
        Console.WriteLine("validate <solution-path>");
        Console.WriteLine("  Validates solution.json and referenced files");
        Console.WriteLine();
        Console.WriteLine("replay <solution-path> <source-run-id> [input]");
        Console.WriteLine("  Replays a prior run using persisted execution records");
        Console.WriteLine();
        Console.WriteLine("fork <solution-path> <source-run-id> <fork-step-index> [input]");
        Console.WriteLine("  Reconstructs state at a step and continues execution from that run context");
    }
}
