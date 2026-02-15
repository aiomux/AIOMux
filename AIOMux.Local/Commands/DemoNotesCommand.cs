using AIOMux.Core;
using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Replay;
using AIOMux.Skills.Notes;
using AIOMux.Skills.Notes.Storage;
using AIOMux.Skills.Notes.Models;
using System.Text.RegularExpressions;

namespace AIOMux.Local.Commands;

/// <summary>
/// Demo commands to exercise NotesSkill + Replay end-to-end.
/// </summary>
public static class DemoNotesCommand
{
    /// <summary>
    /// Execute demo notes commands.
    /// Usage:
    ///   aiomux demo notes add --text "..." [--source "..."] [--replay]
    ///   aiomux demo notes replay --run <runId>
    ///   aiomux demo notes search --q "..."
    /// </summary>
    public static async Task ExecuteAsync(string[] args)
    {
        if (args.Length < 3)
        {
            PrintUsage();
            return;
        }

        var subCommand = args[2].ToLowerInvariant();

        try
        {
            switch (subCommand)
            {
                case "add":
                    await HandleAddAsync(args);
                    break;

                case "replay":
                    await HandleReplayAsync(args);
                    break;

                case "search":
                    await HandleSearchAsync(args);
                    break;

                default:
                    Console.Error.WriteLine($"Unknown demo notes subcommand: {subCommand}");
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

    private static async Task HandleAddAsync(string[] args)
    {
        // Parse arguments
        string? text = null;
        string? source = "demo";
        bool shouldReplay = false;

        for (int i = 3; i < args.Length; i++)
        {
            if (args[i] == "--text" && i + 1 < args.Length)
            {
                text = args[i + 1];
                i++;
            }
            else if (args[i] == "--source" && i + 1 < args.Length)
            {
                source = args[i + 1];
                i++;
            }
            else if (args[i] == "--replay")
            {
                shouldReplay = true;
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            Console.Error.WriteLine("Error: --text argument is required");
            PrintUsage();
            return;
        }

        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine("  NotesSkill Demo with Event Recording");
        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine();

        // Initialize components
        var notesStore = new JsonNotesStore();
        var notesSkill = new NotesSkill(notesStore);
        var recorder = new RunRecorder();

        // Create agent manager and register the skill
        var agentManager = new AgentManager();
        agentManager.Register(notesSkill);

        // Create runtime with recording
        var orchestrator = new AgentOrchestrator(agentManager);
        var baseRuntime = new AgentRuntime(agentManager, orchestrator);
        var recordingRuntime = new RecordingAgentRuntime(baseRuntime, recorder, enableRecording: true);

        // Create execution context
        var context = new AgentContext
        {
            UserInput = text,
            Tools = new Dictionary<string, ITool>
            {
                { "NotesStore", notesStore }
            }
        };

        // Execute with recording
        var request = new AgentRunRequest
        {
            AgentName = "NotesSkill",
            Context = context
        };

        Console.WriteLine($"Input: {text}");
        Console.WriteLine($"Source: {source}");
        Console.WriteLine();
        Console.WriteLine("Executing NotesSkill with event recording...");
        Console.WriteLine();

        var result = await recordingRuntime.RunAsync(request);

        if (!result.Success)
        {
            Console.Error.WriteLine($"Execution failed: {result.Error}");
            return;
        }

        // Extract NoteId from output
        var noteIdMatch = Regex.Match(result.Output, @"ID:\s*([a-f0-9-]+)", RegexOptions.IgnoreCase);
        var noteId = noteIdMatch.Success ? noteIdMatch.Groups[1].Value : "unknown";

        // Get run metadata
        var runs = await recorder.ListRunsAsync();
        var latestRun = runs.FirstOrDefault();

        Console.WriteLine("? Execution completed successfully!");
        Console.WriteLine();
        Console.WriteLine("Results:");
        Console.WriteLine($"  RunId: {latestRun?.RunId ?? "unknown"}");
        Console.WriteLine($"  NoteId: {noteId}");
        Console.WriteLine();
        Console.WriteLine("File Paths:");
        Console.WriteLine($"  Notes: ~/.aiomux/notes.json");
        Console.WriteLine($"  Run Log: ~/.aiomux/runs/{latestRun?.RunId ?? "unknown"}.jsonl");
        Console.WriteLine();
        Console.WriteLine("Output:");
        Console.WriteLine(result.Output);
        Console.WriteLine();

        // Replay if requested
        if (shouldReplay && latestRun != null)
        {
            Console.WriteLine("???????????????????????????????????????????????????");
            Console.WriteLine("  Replaying Recorded Run");
            Console.WriteLine("???????????????????????????????????????????????????");
            Console.WriteLine();
            await ReplayRun(latestRun.RunId);
        }
        else if (latestRun != null)
        {
            Console.WriteLine($"To replay this run:");
            Console.WriteLine($"  aiomux demo notes replay --run {latestRun.RunId}");
        }
    }

    private static async Task HandleReplayAsync(string[] args)
    {
        string? runId = null;

        for (int i = 3; i < args.Length; i++)
        {
            if (args[i] == "--run" && i + 1 < args.Length)
            {
                runId = args[i + 1];
                i++;
            }
        }

        if (string.IsNullOrWhiteSpace(runId))
        {
            Console.Error.WriteLine("Error: --run argument is required");
            PrintUsage();
            return;
        }

        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine("  Replaying Run (Read-Only, No Tool Execution)");
        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine();

        await ReplayRun(runId);
    }

    private static async Task ReplayRun(string runId)
    {
        var engine = new ReplayEngine();
        var result = await engine.ReplayAsync(runId, validateHashes: true);

        if (!result.Success)
        {
            Console.Error.WriteLine($"Replay failed: {result.Error}");
            return;
        }

        Console.WriteLine($"Run: {result.RunId}");
        Console.WriteLine($"Pipeline: {result.PipelineName}");
        Console.WriteLine($"Started: {result.StartTime:yyyy-MM-dd HH:mm:ss UTC}");
        if (result.EndTime.HasValue)
        {
            Console.WriteLine($"Ended: {result.EndTime:yyyy-MM-dd HH:mm:ss UTC}");
            Console.WriteLine($"Duration: {result.TotalDurationMs:F2}ms");
        }
        Console.WriteLine();

        Console.WriteLine("Event Timeline:");
        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine();

        int eventNum = 1;

        // Print events in order
        foreach (var evt in result.Events)
        {
            Console.WriteLine($"{eventNum}. {evt.Type} ({evt.TimestampUtc:HH:mm:ss.fff})");

            if (evt.Type == "RunStarted")
            {
                Console.WriteLine($"   Pipeline: {result.PipelineName}");
                Console.WriteLine($"   Permissions: NotesStore (Save/Get/Search)");
            }
            else if (evt.Type == "InputReceived")
            {
                Console.WriteLine($"   Input: {result.Input}");
                if (!string.IsNullOrEmpty(evt.PayloadHash))
                {
                    Console.WriteLine($"   Hash: {evt.PayloadHash[..16]}...");
                }
            }
            else if (evt.Type == "StepCompleted")
            {
                var step = result.Steps.FirstOrDefault(s =>
                    Math.Abs((s.Timestamp - evt.TimestampUtc).TotalMilliseconds) < 1);
                if (step != null)
                {
                    Console.WriteLine($"   Step: {step.StepName}");
                    Console.WriteLine($"   Output: {TruncateOutput(step.Output, 60)}");
                    Console.WriteLine($"   Duration: {step.DurationMs:F2}ms");
                    if (!string.IsNullOrEmpty(evt.PayloadHash))
                    {
                        Console.WriteLine($"   Hash: {evt.PayloadHash[..16]}...");
                    }
                }
            }
            else if (evt.Type == "ToolInvoked")
            {
                var toolCall = result.ToolCalls.FirstOrDefault(t =>
                    Math.Abs((t.Timestamp - evt.TimestampUtc).TotalMilliseconds) < 1);
                if (toolCall != null)
                {
                    Console.WriteLine($"   Tool: {toolCall.ToolName}");
                    Console.WriteLine($"   Args: {TruncateOutput(toolCall.Args, 60)}");
                    if (!string.IsNullOrEmpty(evt.PayloadHash))
                    {
                        Console.WriteLine($"   ArgsHash: {evt.PayloadHash[..16]}...");
                    }
                }
            }
            else if (evt.Type == "ToolResult")
            {
                var toolCall = result.ToolCalls.FirstOrDefault(t =>
                    t.Result != null &&
                    Math.Abs((t.Timestamp - evt.TimestampUtc).TotalMilliseconds) < 10);
                if (toolCall != null)
                {
                    Console.WriteLine($"   Tool: {toolCall.ToolName}");
                    Console.WriteLine($"   Result: {TruncateOutput(toolCall.Result ?? "", 60)}");
                    Console.WriteLine($"   Duration: {toolCall.DurationMs:F2}ms");
                    Console.WriteLine($"   Success: {toolCall.Success}");
                    if (!string.IsNullOrEmpty(evt.PayloadHash))
                    {
                        Console.WriteLine($"   ResultHash: {evt.PayloadHash[..16]}...");
                    }
                }
            }
            else if (evt.Type == "RunFinished")
            {
                Console.WriteLine($"   Success: {result.Success}");
                if (!string.IsNullOrEmpty(result.FinalOutput))
                {
                    Console.WriteLine($"   Output: {TruncateOutput(result.FinalOutput, 60)}");
                }
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Console.WriteLine($"   Error: {result.Error}");
                }
            }

            Console.WriteLine();
            eventNum++;
        }

        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine($"Total Events: {result.Events.Count}");
        Console.WriteLine($"Pipeline Steps: {result.Steps.Count}");
        Console.WriteLine($"Tool Calls: {result.ToolCalls.Count}");
        Console.WriteLine();
        Console.WriteLine("? Replay completed successfully (no tools executed)");
    }

    private static async Task HandleSearchAsync(string[] args)
    {
        string? query = null;

        for (int i = 3; i < args.Length; i++)
        {
            if (args[i] == "--q" && i + 1 < args.Length)
            {
                query = args[i + 1];
                i++;
            }
        }

        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine("  NotesSkill Search Demo");
        Console.WriteLine("???????????????????????????????????????????????????");
        Console.WriteLine();

        var store = new JsonNotesStore();
        var results = await store.SearchAsync(new NoteQuery
        {
            Q = query,
            MaxResults = 10
        });

        if (results.Count == 0)
        {
            Console.WriteLine("No notes found.");
            return;
        }

        Console.WriteLine($"Found {results.Count} note(s):\n");

        foreach (var note in results)
        {
            Console.WriteLine($"[{note.Type}] {note.Title}");
            Console.WriteLine($"  ID: {note.Id}");
            if (note.Tags.Count > 0)
            {
                Console.WriteLine($"  Tags: {string.Join(", ", note.Tags)}");
            }
            Console.WriteLine($"  Created: {note.CreatedUtc:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }
    }

    private static string TruncateOutput(string output, int maxLength)
    {
        if (string.IsNullOrEmpty(output))
        {
            return "(empty)";
        }

        output = output.Replace("\r\n", " ").Replace("\n", " ");

        if (output.Length <= maxLength)
        {
            return output;
        }

        return output.Substring(0, maxLength - 3) + "...";
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Demo Notes Usage:");
        Console.WriteLine();
        Console.WriteLine("  aiomux demo notes add --text \"<text>\" [--source \"<source>\"] [--replay]");
        Console.WriteLine("  aiomux demo notes replay --run <runId>");
        Console.WriteLine("  aiomux demo notes search --q \"<query>\"");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  aiomux demo notes add --text \"TODO: Review PR #123\" --replay");
        Console.WriteLine("  aiomux demo notes add --text \"Idea: Use Redis caching\" --source \"brainstorm\"");
        Console.WriteLine("  aiomux demo notes replay --run 12345678-1234-1234-1234-123456789abc");
        Console.WriteLine("  aiomux demo notes search --q \"Redis\"");
        Console.WriteLine();
        Console.WriteLine("The 'add' command:");
        Console.WriteLine("  - Executes NotesSkill with event recording");
        Console.WriteLine("  - Persists note to ~/.aiomux/notes.json");
        Console.WriteLine("  - Records run log to ~/.aiomux/runs/<runId>.jsonl");
        Console.WriteLine("  - Use --replay to immediately show the event timeline");
        Console.WriteLine();
        Console.WriteLine("The 'replay' command:");
        Console.WriteLine("  - Loads and validates run events from JSONL");
        Console.WriteLine("  - Reconstructs execution timeline (NO tool execution, NO LLM calls)");
        Console.WriteLine("  - Proves determinism and permission enforcement");
    }
}
