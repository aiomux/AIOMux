using AIOMux.Core.Models;
using System.Text.Json;

namespace AIOMux.Core.Replay;

internal static class ForkReplayHelper
{
    public static async Task<ReplayResult> LoadReplayAsync(string runId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var engine = new ReplayEngine();
        return await engine.ReplayAsync(runId);
    }

    public static bool TryHydrateContext(AgentContext context, IReadOnlyList<RuntimeEvent> events, int eventIndex, out string? error)
    {
        error = null;

        if (eventIndex < 0 || eventIndex >= events.Count)
        {
            error = $"Event index {eventIndex} is out of range.";
            return false;
        }

        for (int i = 0; i <= eventIndex; i++)
        {
            var evt = events[i];

            if (evt.Type == "InputReceived")
            {
                var payload = GetPayload<InputReceivedEvent.InputReceivedPayload>(evt);
                if (payload != null)
                {
                    context.UserInput = payload.Input;
                    if (!context.Variables.ContainsKey("user.input.original"))
                    {
                        context.Variables["user.input.original"] = payload.Input;
                    }
                }
            }
            else if (evt.Type == "StepStarted")
            {
                var payload = GetPayload<StepStartedEvent.StepStartedPayload>(evt);
                if (payload != null)
                {
                    context.Variables["stepIndex"] = payload.StepIndex;
                }
            }
            else if (evt.Type == "StepCompleted")
            {
                var payload = GetPayload<StepCompletedEvent.StepCompletedPayload>(evt);
                if (payload != null)
                {
                    if (!string.IsNullOrWhiteSpace(payload.StepName))
                    {
                        context.Variables[payload.StepName] = payload.Output;
                    }
                    context.Variables["stepIndex"] = payload.StepIndex;
                }
            }
        }

        return true;
    }

    public static IReplaySource? BuildReplaySource(string sourceRunId, string newRunId)
    {
        var filePath = GetRunFilePath(sourceRunId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        var replaySource = new InMemoryReplaySource();
        var callIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
        int? currentStepIndex = null;

        foreach (var line in File.ReadLines(filePath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            if (!doc.RootElement.TryGetProperty("Type", out var typeElement))
            {
                continue;
            }

            var eventType = typeElement.GetString();
            if (string.Equals(eventType, "StepStarted", StringComparison.Ordinal))
            {
                if (TryGetPayloadProperty(doc.RootElement, "StepIndex", out var stepIndexElement))
                {
                    currentStepIndex = stepIndexElement.GetInt32();
                }
            }
            else if (string.Equals(eventType, "StepCompleted", StringComparison.Ordinal))
            {
                if (TryGetPayloadProperty(doc.RootElement, "StepIndex", out var stepIndexElement))
                {
                    currentStepIndex = stepIndexElement.GetInt32();
                }
            }
            else if (string.Equals(eventType, "ToolProposed", StringComparison.Ordinal))
            {
                if (!doc.RootElement.TryGetProperty("Payload", out var payloadElement))
                {
                    continue;
                }

                if (!payloadElement.TryGetProperty("CallId", out var callIdElement) ||
                    !payloadElement.TryGetProperty("ToolName", out var toolNameElement) ||
                    !payloadElement.TryGetProperty("JsonArgs", out var jsonArgsElement))
                {
                    continue;
                }

                var oldCallId = callIdElement.GetString();
                var toolName = toolNameElement.GetString();
                var jsonArgs = jsonArgsElement.GetString();

                if (string.IsNullOrWhiteSpace(oldCallId) || string.IsNullOrWhiteSpace(toolName) || jsonArgs == null)
                {
                    continue;
                }

                var stepIndexText = currentStepIndex?.ToString() ?? string.Empty;
                var newCallId = DeterministicCallId.Generate(newRunId, stepIndexText, toolName, jsonArgs);
                callIdMap[oldCallId] = newCallId;
            }
            else if (string.Equals(eventType, "ToolResult", StringComparison.Ordinal))
            {
                if (!doc.RootElement.TryGetProperty("Result", out var resultElement))
                {
                    continue;
                }

                var result = resultElement.Deserialize<ToolResult>();
                if (result == null || string.IsNullOrWhiteSpace(result.CallId))
                {
                    continue;
                }

                if (!callIdMap.TryGetValue(result.CallId, out var newCallId))
                {
                    continue;
                }

                replaySource.AddToolResult(newCallId, new ToolResult
                {
                    CallId = newCallId,
                    Success = result.Success,
                    Error = result.Error,
                    JsonResult = result.JsonResult
                });
            }
        }

        return replaySource;
    }

    private static T? GetPayload<T>(RuntimeEvent evt)
    {
        if (evt.Payload is JsonElement element)
        {
            return element.Deserialize<T>();
        }

        return evt.Payload is T typed ? typed : default;
    }

    private static bool TryGetPayloadProperty(JsonElement root, string propertyName, out JsonElement value)
    {
        value = default;
        return root.TryGetProperty("Payload", out var payloadElement) &&
               payloadElement.TryGetProperty(propertyName, out value);
    }

    private static string GetRunFilePath(string runId)
    {
        var runsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aiomux",
            "runs");
        return Path.Combine(runsDirectory, $"{runId}.jsonl");
    }
}
