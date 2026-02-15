using AIOMux.Core.Replay;

namespace AIOMux.Local.Commands;

/// <summary>
/// Handles the 'run' subcommands for listing, showing, and replaying recorded runs.
/// </summary>
public static class RunManagementCommand
{
    /// <summary>
    /// Execute run management command.
    /// Usage:
    ///   aiomux run list
    ///   aiomux run show <runId>
    ///   aiomux run replay <runId>
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
            switch (subCommand)
            {
                case "list":
                    await HandleListAsync();
                    break;

                case "show":
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Error: runId is required");
                        PrintUsage();
                        return;
                    }
                    await HandleShowAsync(args[2]);
                    break;

                case "replay":
                    if (args.Length < 3)
                    {
                        Console.Error.WriteLine("Error: runId is required");
                        PrintUsage();
                        return;
                    }
                    await HandleReplayAsync(args[2]);
                    break;

                default:
                    Console.Error.WriteLine($"Unknown run subcommand: {subCommand}");
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

    private static async Task HandleListAsync()
    {
        var recorder = new RunRecorder();
        var runs = await recorder.ListRunsAsync();

        if (runs.Count == 0)
        {
            Console.WriteLine("No recorded runs found.");
            return;
        }

        Console.WriteLine($"Found {runs.Count} recorded run(s):\n");

        foreach (var run in runs)
        {
            var status = run.Success.HasValue
                ? (run.Success.Value ? "? Success" : "? Failed")
                : "? Incomplete";

            var duration = run.CompletedUtc.HasValue
                ? (run.CompletedUtc.Value - run.StartedUtc).TotalSeconds
                : 0;

            Console.WriteLine($"[{run.RunId[..8]}...] {run.PipelineName ?? "unknown"}");
            Console.WriteLine($"  Status: {status}");
            Console.WriteLine($"  Started: {run.StartedUtc:yyyy-MM-dd HH:mm:ss}");
            if (run.CompletedUtc.HasValue)
            {
                Console.WriteLine($"  Duration: {duration:F2}s");
            }
            Console.WriteLine($"  Events: {run.EventCount}");
            Console.WriteLine();
        }

        Console.WriteLine($"Runs are stored in: ~/.aiomux/runs/");
    }

    private static async Task HandleShowAsync(string runId)
    {
        var recorder = new RunRecorder();
        var metadata = await recorder.GetRunMetadataAsync(runId);

        if (metadata == null)
        {
            Console.Error.WriteLine($"Run not found: {runId}");
            return;
        }

        Console.WriteLine($"Run: {metadata.RunId}");
        Console.WriteLine($"Pipeline: {metadata.PipelineName ?? "unknown"}");
        Console.WriteLine($"Started: {metadata.StartedUtc:yyyy-MM-dd HH:mm:ss UTC}");

        if (metadata.CompletedUtc.HasValue)
        {
            var duration = (metadata.CompletedUtc.Value - metadata.StartedUtc).TotalSeconds;
            Console.WriteLine($"Completed: {metadata.CompletedUtc:yyyy-MM-dd HH:mm:ss UTC}");
            Console.WriteLine($"Duration: {duration:F2}s");
        }
        else
        {
            Console.WriteLine("Status: Incomplete");
        }

        if (metadata.Success.HasValue)
        {
            Console.WriteLine($"Success: {(metadata.Success.Value ? "Yes" : "No")}");
        }

        Console.WriteLine($"Events: {metadata.EventCount}");
        Console.WriteLine();
        Console.WriteLine("To replay this run:");
        Console.WriteLine($"  aiomux run replay {runId}");
    }

    private static async Task HandleReplayAsync(string runId)
    {
        Console.WriteLine($"Replaying run: {runId}\n");

        var engine = new ReplayEngine();
        var result = await engine.ReplayAsync(runId);

        if (!result.Success)
        {
            Console.Error.WriteLine($"Replay failed: {result.Error}");
            return;
        }

        // Print replay results
        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine($"  Run: {result.RunId}");
        Console.WriteLine($"  Pipeline: {result.PipelineName}");
        Console.WriteLine($"  Started: {result.StartTime:yyyy-MM-dd HH:mm:ss UTC}");
        if (result.EndTime.HasValue)
        {
            Console.WriteLine($"  Ended: {result.EndTime:yyyy-MM-dd HH:mm:ss UTC}");
            Console.WriteLine($"  Duration: {result.TotalDurationMs:F2}ms");
        }
        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine();

        // Print input
        if (!string.IsNullOrEmpty(result.Input))
        {
            Console.WriteLine("Input:");
            Console.WriteLine($"  {result.Input}");
            Console.WriteLine();
        }

        // Print steps
        if (result.Steps.Count > 0)
        {
            Console.WriteLine("Pipeline Steps:");
            for (int i = 0; i < result.Steps.Count; i++)
            {
                var step = result.Steps[i];
                Console.WriteLine($"  [{i + 1}] {step.StepName} ({step.DurationMs:F2}ms)");
                Console.WriteLine($"      Output: {TruncateOutput(step.Output, 100)}");
            }
            Console.WriteLine();
        }

        // Print tool calls
        if (result.ToolCalls.Count > 0)
        {
            Console.WriteLine("Tool Calls:");
            for (int i = 0; i < result.ToolCalls.Count; i++)
            {
                var call = result.ToolCalls[i];
                var status = call.Success ? "?" : "?";
                Console.WriteLine($"  [{i + 1}] {status} {call.ToolName} ({call.DurationMs:F2}ms)");
                Console.WriteLine($"      Args: {TruncateOutput(call.Args, 80)}");
                if (!string.IsNullOrEmpty(call.Result))
                {
                    Console.WriteLine($"      Result: {TruncateOutput(call.Result, 80)}");
                }
                if (!string.IsNullOrEmpty(call.Error))
                {
                    Console.WriteLine($"      Error: {call.Error}");
                }
            }
            Console.WriteLine();
        }

        // Print final output
        if (!string.IsNullOrEmpty(result.FinalOutput))
        {
            Console.WriteLine("Final Output:");
            Console.WriteLine($"  {result.FinalOutput}");
            Console.WriteLine();
        }

        // Print error if any
        if (!string.IsNullOrEmpty(result.Error))
        {
            Console.WriteLine($"Error: {result.Error}");
            Console.WriteLine();
        }

        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine($"Total Events: {result.Events.Count}");
        Console.WriteLine("???????????????????????????????????????????????????");
    }

    private static string TruncateOutput(string output, int maxLength)
    {
        if (string.IsNullOrEmpty(output))
        {
            return "(empty)";
        }

        // Replace newlines with spaces
        output = output.Replace("\r\n", " ").Replace("\n", " ");

        if (output.Length <= maxLength)
        {
            return output;
        }

        return output.Substring(0, maxLength - 3) + "...";
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Run Management Usage:");
        Console.WriteLine();
        Console.WriteLine("  aiomux run list                 - List all recorded runs");
        Console.WriteLine("  aiomux run show <runId>         - Show details of a run");
        Console.WriteLine("  aiomux run replay <runId>       - Replay a recorded run");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  aiomux run list");
        Console.WriteLine("  aiomux run show 12345678-1234-1234-1234-123456789abc");
        Console.WriteLine("  aiomux run replay 12345678-1234-1234-1234-123456789abc");
    }
}
