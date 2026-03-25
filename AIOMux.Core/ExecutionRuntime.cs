using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace AIOMux.Core;

/// <summary>
/// Canonical orchestration engine. Executes an <see cref="ExecutionPlan"/> step by step,
/// routes each step to its agent or tool, mutates state, and records all trace data as <see cref="ExecutionRecord"/>.
/// </summary>
public class ExecutionRuntime : IExecutionRuntime
{
    private readonly ILogger<ExecutionRuntime> _logger;

    public ExecutionRuntime(ILogger<ExecutionRuntime>? logger = null)
    {
        _logger = logger ?? NullLogger<ExecutionRuntime>.Instance;
    }

    /// <summary>
    /// Executes an execution plan with the provided context.
    /// Runtime services are read from <see cref="ExecutionContext.Services"/>.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(
        ExecutionPlan plan,
        ExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        ExecutionContext? activeContext = null;

        try
        {
            if (plan == null)
                return Fail("Plan cannot be null");
            if (context == null)
                return Fail("Context cannot be null");

            var ctx = context;
            activeContext = ctx;
            var services = ctx.Services ?? new ExecutionRuntimeServices();
            ctx.Services = services;

            if (!ctx.State.ContainsKey("input"))
                ctx.State["input"] = ctx.Inputs.TryGetValue("input", out var entry) ? entry?.ToString() ?? string.Empty : string.Empty;
            if (!ctx.State.ContainsKey("user.input.original"))
                ctx.State["user.input.original"] = ctx.State["input"];

            PublishAvailableAgents(services, ctx);

            _logger.LogInformation("Starting plan '{PlanName}' ({StepCount} steps)", plan.Name, plan.Steps.Count);

            string output = string.Empty;

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = plan.Steps[i];
                ctx.State["stepIndex"] = i;

                var stepStart = DateTimeOffset.UtcNow;
                var stepSw = Stopwatch.StartNew();

                try
                {
                    var stepResult = await ExecuteStepAsync(step, ctx, services, cancellationToken);
                    stepSw.Stop();

                    if (!stepResult.Success)
                    {
                        var msg = stepResult.Error ?? "Step execution failed";
                        _logger.LogWarning("Step {StepId} ({Target}) denied or failed: {Message}", step.Id, step.Target, msg);

                        ctx.Records.Add(new ExecutionRecord
                        {
                            RunId = ctx.RunId,
                            StepId = step.Id,
                            StepIndex = i,
                            Type = step.Type,
                            Target = step.Target,
                            Input = ctx.GetInput(),
                            Success = false,
                            Error = msg,
                            PolicyDenyReason = stepResult.PolicyDenyReason,
                            PolicyHash = stepResult.PolicyHash,
                            Timestamp = stepStart,
                            DurationMs = stepSw.Elapsed.TotalMilliseconds
                        });

                        return new ExecutionResult { Success = false, Error = msg, StepIndex = i, StepId = step.Id };
                    }

                    output = stepResult.Output;
                    var outputKey = step.OutputKey ?? step.Target;
                    var stateChanges = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        [outputKey] = output
                    };

                    ctx.State[outputKey] = output;

                    foreach (var (key, value) in stepResult.Outputs)
                    {
                        ctx.State[key] = value;
                        stateChanges[key] = value;
                    }

                    ctx.Records.Add(new ExecutionRecord
                    {
                        RunId = ctx.RunId,
                        StepId = step.Id,
                        StepIndex = i,
                        Type = step.Type,
                        Target = step.Target,
                        Input = ctx.GetInput(),
                        Output = output,
                        OutputKey = outputKey,
                        StateChanges = stateChanges,
                        Success = true,
                        Timestamp = stepStart,
                        DurationMs = stepSw.Elapsed.TotalMilliseconds
                    });

                    _logger.LogInformation("Step {StepId} ({Target}) completed in {Ms:F1}ms", step.Id, step.Target, stepSw.Elapsed.TotalMilliseconds);
                }
                catch (OperationCanceledException)
                {
                    stepSw.Stop();
                    var msg = $"Execution cancelled at step {step.Id} ({step.Target})";

                    ctx.Records.Add(new ExecutionRecord
                    {
                        RunId = ctx.RunId,
                        StepId = step.Id,
                        StepIndex = i,
                        Type = step.Type,
                        Target = step.Target,
                        Input = ctx.GetInput(),
                        Success = false,
                        Error = msg,
                        Timestamp = stepStart,
                        DurationMs = stepSw.Elapsed.TotalMilliseconds
                    });

                    return new ExecutionResult { Success = false, Error = msg, StepIndex = i, StepId = step.Id };
                }
                catch (Exception ex)
                {
                    stepSw.Stop();
                    var msg = $"Error in step {step.Id} ({step.Target}): {ex.Message}";
                    _logger.LogError(ex, msg);

                    ctx.Records.Add(new ExecutionRecord
                    {
                        RunId = ctx.RunId,
                        StepId = step.Id,
                        StepIndex = i,
                        Type = step.Type,
                        Target = step.Target,
                        Input = ctx.GetInput(),
                        Success = false,
                        Error = ex.Message,
                        Timestamp = stepStart,
                        DurationMs = stepSw.Elapsed.TotalMilliseconds
                    });

                    return new ExecutionResult { Success = false, Error = msg, StepIndex = i, StepId = step.Id };
                }
            }

            if (services.Options.GenerateJobSummary && services.Options.CollectMetrics)
                output = AppendSummary(output, ctx, plan.Name, services.Options.IncludeDetailedMetrics);

            sw.Stop();
            _logger.LogInformation("Plan '{PlanName}' completed in {Ms:F1}ms", plan.Name, sw.Elapsed.TotalMilliseconds);
            return new ExecutionResult { Success = true, Output = output };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult { Success = false, Error = "Execution was cancelled" };
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error during plan execution: {ex.Message}";
            _logger.LogError(ex, error);
            return new ExecutionResult { Success = false, Error = error };
        }
        finally
        {
            if (activeContext != null)
                await PersistRecordsAsync(activeContext, cancellationToken);
        }
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        ExecutionStep step,
        ExecutionContext ctx,
        ExecutionRuntimeServices services,
        CancellationToken ct)
    {
        var resolvedInputs = StepInputResolver.ResolveInputs(step, ctx);
        ctx.State["input"] = StepInputResolver.GetInputString(resolvedInputs, "input");

        var stepMetadata = new ExecutionStepMetadata
        {
            StepId = step.Id,
            Type = step.Type,
            Target = step.Target
        };

        var policyDecision = services.PolicyEngine.EvaluateStep(stepMetadata, resolvedInputs, ctx);

        if (!policyDecision.Allowed)
        {
            var msg = policyDecision.DenyReason ?? "Step execution denied by policy.";
            return new StepExecutionResult
            {
                Success = false,
                Error = msg,
                PolicyDenyReason = policyDecision.DenyReason,
                PolicyHash = policyDecision.PolicyHash
            };
        }

        if (step.Type == "tool")
            return await ExecuteToolStepAsync(step, resolvedInputs, services, ctx, ct);

        if (step.Type == "agent")
        {
            if (services.AgentManager == null)
                throw new InvalidOperationException($"AgentManager is required to execute agent step '{step.Id}'");

            var agent = services.AgentManager.GetByName(step.Target)
                ?? throw new InvalidOperationException($"Agent not found: '{step.Target}'");

            return await agent.ExecuteAsync(resolvedInputs, ctx, ct);
        }

        throw new InvalidOperationException($"Unknown step type: '{step.Type}'");
    }

    private async Task<StepExecutionResult> ExecuteToolStepAsync(
        ExecutionStep step,
        ImmutableDictionary<string, object?> resolvedInputs,
        ExecutionRuntimeServices services,
        ExecutionContext ctx,
        CancellationToken ct)
    {
        var input = StepInputResolver.GetInputString(resolvedInputs, "input");
        ctx.State["input"] = input;

        var stepIndex = ctx.State.TryGetValue("stepIndex", out var stepIndexValue)
            ? stepIndexValue?.ToString() ?? string.Empty
            : string.Empty;

        var callId = DeterministicCallId.Generate(ctx.RunId, stepIndex, step.Target, input);
        var replayKey = DeterministicCallId.GenerateReplayKey(stepIndex, step.Target, input);

        ToolResult toolResult;
        if ((services.ReplayMode == ReplayMode.Full || services.ReplayMode == ReplayMode.ToolsOnly)
            && services.ReplayToolResults.TryGetValue(replayKey, out var replayedResult))
        {
            toolResult = new ToolResult
            {
                CallId = callId,
                Success = replayedResult.Success,
                Error = replayedResult.Error,
                JsonResult = replayedResult.JsonResult
            };
        }
        else
        {
            if (!services.Tools.TryGetValue(step.Target, out var tool))
            {
                return new StepExecutionResult { Success = false, Error = $"Tool not found: {step.Target}" };
            }

            ct.ThrowIfCancellationRequested();

            try
            {
                var output = await tool.ExecuteAsync(input);
                toolResult = new ToolResult
                {
                    CallId = callId,
                    Success = true,
                    JsonResult = output
                };
            }
            catch (Exception ex)
            {
                toolResult = new ToolResult
                {
                    CallId = callId,
                    Success = false,
                    Error = ex.Message,
                    JsonResult = string.Empty
                };
            }
        }

        if (!toolResult.Success)
            throw new InvalidOperationException(toolResult.Error ?? $"Tool '{step.Target}' failed");

        return new StepExecutionResult { Success = true, Output = toolResult.JsonResult };
    }

    private static string AppendSummary(string output, ExecutionContext ctx, string planName, bool includeDetailedMetrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- Plan Summary: {planName} ---");

        double total = 0;
        foreach (var r in ctx.Records)
        {
            total += r.DurationMs;
            if (includeDetailedMetrics)
                sb.AppendLine($"  - {r.StepId} ({r.Target}): {r.DurationMs:F2} ms");
        }

        sb.AppendLine($"Total: {total:F2} ms");
        return $"{output}\n\n{sb}";
    }

    private static ExecutionResult Fail(string? error)
        => new() { Success = false, Error = error };

    private static async Task PersistRecordsAsync(ExecutionContext ctx, CancellationToken cancellationToken)
    {
        try
        {
            await ExecutionRecordStore.SaveAsync(ctx.RunId, ctx.Records, cancellationToken);
        }
        catch
        {
            // Persistence failures should not mask execution results.
        }
    }

    private static void PublishAvailableAgents(ExecutionRuntimeServices services, ExecutionContext context)
    {
        if (services.AgentManager == null)
            return;

        var agents = services.AgentManager
            .GetAllAgents()
            .Where(a => !a.Name.Equals("PlannerAgent", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (agents.Count == 0)
            return;

        context.State["AvailableAgents"] = string.Join(Environment.NewLine, agents.Select(a => $"- {a}"));
    }
}
