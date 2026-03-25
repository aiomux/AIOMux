using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using System.Text.Json;

namespace AIOMux.Core;

/// <summary>
/// LLM-backed planner agent that produces dynamic step JSON.
/// </summary>
public sealed class PlannerAgent : IAgent
{
    private readonly ILLMClient? _llmClient;

    public string Name => "PlannerAgent";

    public PlannerAgent(ILLMClient? llmClient = null)
    {
        _llmClient = llmClient;
    }

    public async Task<StepExecutionResult> ExecuteAsync(
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var availableAgents = GetAvailableAgents(context);
        if (availableAgents.Count == 0)
            return new StepExecutionResult { Success = true, Output = "[]" };

        if (_llmClient == null)
            return new StepExecutionResult { Success = true, Output = BuildFallbackPlanJson(availableAgents[0]) };

        var systemPrompt = BuildSystemPrompt(availableAgents);
        var raw = await _llmClient.CompleteAsync(context.GetInput(), systemPrompt);
        var json = TryExtractJsonArray(raw);

        if (TryValidatePlan(json, availableAgents, out var validated))
            return new StepExecutionResult { Success = true, Output = validated };

        return new StepExecutionResult { Success = true, Output = BuildFallbackPlanJson(availableAgents[0]) };
    }

    private static List<string> GetAvailableAgents(ExecutionContext context)
    {
        if (context.AgentManager != null)
        {
            return context.AgentManager
                .GetAllAgents()
                .Select(a => a.Name)
                .Where(n => !n.Equals("PlannerAgent", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (context.State.TryGetValue("AvailableAgents", out var value) && value is string lines)
        {
            return lines.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim().TrimStart('-').Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return [];
    }

    private static string BuildSystemPrompt(IReadOnlyCollection<string> availableAgents)
    {
        return $"""
You are a planning agent for an agent-orchestration runtime.
Return ONLY JSON.
Output must be a JSON array of steps where each step has:
- agentName (required)
- inputFrom (optional)
- outputTo (optional)

Constraints:
- Use only these agent names: {string.Join(", ", availableAgents)}
- Usually create the minimal chain needed.
- If unsure, create one step with the best matching agent and inputFrom='user'.
- Do not include markdown code fences.
""";
    }

    private static string TryExtractJsonArray(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var start = raw.IndexOf('[');
        var end = raw.LastIndexOf(']');
        if (start >= 0 && end > start)
            return raw[start..(end + 1)];

        return raw.Trim();
    }

    private static bool TryValidatePlan(string candidateJson, IReadOnlyCollection<string> availableAgents, out string validatedJson)
    {
        validatedJson = string.Empty;
        if (string.IsNullOrWhiteSpace(candidateJson))
            return false;

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var steps = JsonSerializer.Deserialize<List<PlanStep>>(candidateJson, options);
            if (steps == null || steps.Count == 0)
                return false;

            var allowed = new HashSet<string>(availableAgents, StringComparer.OrdinalIgnoreCase);
            foreach (var step in steps)
            {
                if (string.IsNullOrWhiteSpace(step.AgentName) || !allowed.Contains(step.AgentName))
                    return false;

                if (string.IsNullOrWhiteSpace(step.InputFrom))
                    step.InputFrom = "user";
            }

            validatedJson = JsonSerializer.Serialize(steps);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string BuildFallbackPlanJson(string agentName)
    {
        var steps = new List<PlanStep>
        {
            new() { AgentName = agentName, InputFrom = "user", OutputTo = agentName }
        };
        return JsonSerializer.Serialize(steps);
    }

    // Local DTO used for JSON serialization within PlannerAgent.
    private sealed class PlanStep
    {
        public string AgentName { get; set; } = string.Empty;
        public string? InputFrom { get; set; }
        public string? OutputTo { get; set; }
    }
}
