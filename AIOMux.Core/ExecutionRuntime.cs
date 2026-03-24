using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AIOMux.Core;

/// <summary>
/// Canonical orchestration engine. Executes an <see cref="ExecutionPlan"/> step by step,
/// routing each step to its agent or tool, managing state, emitting events, and recording traces.
/// </summary>
public class ExecutionRuntime : IExecutionRuntime
{
    private readonly ILogger<ExecutionRuntime> _logger;
    private readonly IRuntimeEventSink? _eventSink;

    public ExecutionRuntime(ILogger<ExecutionRuntime>? logger = null, IRuntimeEventSink? eventSink = null)
    {
        _logger = logger ?? NullLogger<ExecutionRuntime>.Instance;
        _eventSink = eventSink;
    }

    // ── RunAsync ──────────────────────────────────────────────────────────────

    public async Task<ExecutionResult> RunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (request == null)
                return Fail("Request cannot be null");
            if (request.Plan == null)
                return Fail("Plan cannot be null");
            if (request.Context == null)
                return Fail("Context cannot be null");

            var plan = request.Plan;
            var ctx = request.Context;

            // Seed State["input"] and preserve the original entry input
            if (!ctx.State.ContainsKey("input"))
                ctx.State["input"] = ctx.Inputs.TryGetValue("input", out var entry) ? entry?.ToString() ?? string.Empty : string.Empty;
            if (!ctx.State.ContainsKey("user.input.original"))
                ctx.State["user.input.original"] = ctx.State["input"];

            await EmitAsync(new RunStartedEvent
            {
                Payload = new RunStartedEvent.RunStartedPayload
                {
                    PipelineName = plan.Name,
                    WorkingDirectory = ctx.WorkingDirectory
                }
            }, cancellationToken);

            _logger.LogInformation("Starting plan '{PlanName}' ({StepCount} steps)", plan.Name, plan.Steps.Count);

            string output = string.Empty;

            for (int i = 0; i < plan.Steps.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = plan.Steps[i];
                ctx.State["stepIndex"] = i;

                await EmitAsync(new StepStartedEvent
                {
                    Payload = new StepStartedEvent.StepStartedPayload
                    {
                        RunId = ctx.RunId,
                        StepIndex = i,
                        StepTarget = step.Target
                    }
                }, cancellationToken);

                var stepStart = DateTimeOffset.UtcNow;
                var stepSw = Stopwatch.StartNew();

                try
                {
                    ApplyBindings(step, ctx);
                    var stepResult = await ExecuteStepAsync(step, ctx, cancellationToken);
                    stepSw.Stop();

                    output = stepResult.Output;
                    var outputKey = step.OutputKey ?? step.Target;
                    ctx.State[outputKey] = output;

                    foreach (var (key, value) in stepResult.Outputs)
                        ctx.State[key] = value;

                    ctx.Records.Add(new ExecutionRecord
                    {
                        RunId = ctx.RunId,
                        StepId = step.Id,
                        Type = step.Type,
                        Target = step.Target,
                        Input = ctx.GetInput(),
                        Output = output,
                        Success = true,
                        Timestamp = stepStart,
                        DurationMs = stepSw.Elapsed.TotalMilliseconds
                    });

                    await EmitAsync(new StepCompletedEvent
                    {
                        Payload = new StepCompletedEvent.StepCompletedPayload
                        {
                            RunId = ctx.RunId,
                            StepIndex = i,
                            StepTarget = step.Target,
                            StepName = outputKey,
                            Output = output,
                            OutputHash = ComputeHash(output),
                            DurationMs = stepSw.Elapsed.TotalMilliseconds
                        }
                    }, cancellationToken);

                    _logger.LogInformation("Step {StepId} ({Target}) completed in {Ms:F1}ms",
                        step.Id, step.Target, stepSw.Elapsed.TotalMilliseconds);
                }
                catch (OperationCanceledException)
                {
                    stepSw.Stop();
                    var msg = $"Execution cancelled at step {step.Id} ({step.Target})";
                    _logger.LogWarning(msg);

                    ctx.Records.Add(new ExecutionRecord
                    {
                        RunId = ctx.RunId,
                        StepId = step.Id,
                        Type = step.Type,
                        Target = step.Target,
                        Input = ctx.GetInput(),
                        Success = false,
                        Error = msg,
                        Timestamp = stepStart,
                        DurationMs = stepSw.Elapsed.TotalMilliseconds
                    });

                    await EmitStepFailedAsync(ctx.RunId, i, step.Target, msg, cancellationToken);
                    await EmitRunFinishedAsync(false, null, msg, sw.Elapsed.TotalMilliseconds, cancellationToken);
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
                        Type = step.Type,
                        Target = step.Target,
                        Input = ctx.GetInput(),
                        Success = false,
                        Error = ex.Message,
                        Timestamp = stepStart,
                        DurationMs = stepSw.Elapsed.TotalMilliseconds
                    });

                    await EmitStepFailedAsync(ctx.RunId, i, step.Target, ex.Message, cancellationToken);
                    await EmitRunFinishedAsync(false, null, msg, sw.Elapsed.TotalMilliseconds, cancellationToken);
                    return new ExecutionResult { Success = false, Error = msg, StepIndex = i, StepId = step.Id };
                }
            }

            // Optionally append a summary report
            if (ctx.Options.GenerateJobSummary && ctx.Options.CollectMetrics)
                output = AppendSummary(output, ctx, plan.Name);

            sw.Stop();
            _logger.LogInformation("Plan '{PlanName}' completed in {Ms:F1}ms", plan.Name, sw.Elapsed.TotalMilliseconds);
            await EmitRunFinishedAsync(true, output, null, sw.Elapsed.TotalMilliseconds, cancellationToken);
            return new ExecutionResult { Success = true, Output = output };
        }
        catch (OperationCanceledException)
        {
            var error = "Execution was cancelled";
            _logger.LogWarning(error);
            await EmitRunFinishedAsync(false, null, error, sw.Elapsed.TotalMilliseconds, cancellationToken);
            return new ExecutionResult { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error during plan execution: {ex.Message}";
            _logger.LogError(ex, error);
            await EmitRunFinishedAsync(false, null, error, sw.Elapsed.TotalMilliseconds, CancellationToken.None);
            return new ExecutionResult { Success = false, Error = error };
        }
    }

    // ── ForkAsync ─────────────────────────────────────────────────────────────

    public async Task<ExecutionResult> ForkAsync(ExecutionForkRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (request == null)
                return Fail("Fork request cannot be null");
            if (string.IsNullOrWhiteSpace(request.SourceRunId))
                return Fail("SourceRunId is required for fork execution");
            if (request.Context == null)
                return Fail("Fork request context cannot be null");
            if (request.Plan == null)
                return Fail("Fork request plan cannot be null");

            var ctx = request.Context;

            var replayResult = await ForkReplayHelper.LoadReplayAsync(request.SourceRunId, cancellationToken);
            if (!replayResult.Success)
                return Fail(replayResult.Error ?? "Failed to load replay data");

            if (!ForkReplayHelper.TryHydrateContext(ctx, replayResult.Events, request.EventIndex, out var hydrateError))
                return Fail(hydrateError);

            ctx.State["fork.sourceRunId"] = request.SourceRunId;
            ctx.State["fork.eventIndex"] = request.EventIndex;
            ctx.ReplayMode = request.ReplayMode;

            if (request.ReplayMode != ReplayMode.None)
            {
                ctx.ReplaySource = ForkReplayHelper.BuildReplaySource(request.SourceRunId, ctx.RunId, request.EventIndex);
                if (ctx.ReplaySource == null)
                    return Fail($"Unable to build replay source for run '{request.SourceRunId}'");
            }

            ctx.ToolDispatcher = new ToolDispatcher(_eventSink ?? new NullRuntimeEventSink(), request.PolicyEngine ?? new AllowAllPolicyEngine());

            return await RunAsync(
                new ExecutionRunRequest { Plan = request.Plan, Context = ctx },
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            var error = "Fork execution was cancelled";
            _logger.LogWarning(error);
            return new ExecutionResult { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error in fork execution: {ex.Message}";
            _logger.LogError(ex, error);
            return new ExecutionResult { Success = false, Error = error };
        }
    }

    // ── Step execution ────────────────────────────────────────────────────────

    private static void ApplyBindings(ExecutionStep step, ExecutionContext ctx)
    {
        if (!step.Bindings.TryGetValue("input", out var source))
            return;

        if (source == "user")
        {
            if (ctx.State.TryGetValue("user", out var userVal) && userVal is not null)
                ctx.State["input"] = userVal.ToString()!;
        }
        else if (ctx.State.TryGetValue(source, out var stateVal))
        {
            ctx.State["input"] = stateVal?.ToString() ?? string.Empty;
        }
    }

    private async Task<StepExecutionResult> ExecuteStepAsync(
        ExecutionStep step,
        ExecutionContext ctx,
        CancellationToken ct)
    {
        if (step.Type == "tool")
        {
            var input = step.Inputs.TryGetValue("input", out var inputVal)
                ? inputVal?.ToString() ?? ctx.GetInput()
                : ctx.GetInput();

            var result = await ctx.ExecuteToolAsync(step.Target, input, ct);
            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? $"Tool '{step.Target}' failed");

            return new StepExecutionResult { Success = true, Output = result.JsonResult };
        }

        if (step.Type == "agent")
        {
            if (ctx.AgentManager == null)
                throw new InvalidOperationException($"AgentManager is required to execute agent step '{step.Id}'");

            var agent = ctx.AgentManager.GetByName(step.Target)
                ?? throw new InvalidOperationException($"Agent not found: '{step.Target}'");

            return await agent.ExecuteAsync(ctx, ct);
        }

        throw new InvalidOperationException($"Unknown step type: '{step.Type}'");
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    private static string AppendSummary(string output, ExecutionContext ctx, string planName)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"--- Plan Summary: {planName} ---");

        double total = 0;
        foreach (var r in ctx.Records)
        {
            total += r.DurationMs;
            if (ctx.Options.IncludeDetailedMetrics)
                sb.AppendLine($"  - {r.StepId} ({r.Target}): {r.DurationMs:F2} ms");
        }

        sb.AppendLine($"Total: {total:F2} ms");
        return $"{output}\n\n{sb}";
    }

    // ── Event helpers ─────────────────────────────────────────────────────────

    private async Task EmitAsync(RuntimeEvent evt, CancellationToken ct)
    {
        if (_eventSink != null)
            await _eventSink.RecordAsync(evt, ct);
    }

    private async Task EmitStepFailedAsync(string runId, int stepIndex, string target, string message, CancellationToken ct)
    {
        await EmitAsync(new StepFailedEvent
        {
            Payload = new StepFailedEvent.StepFailedPayload
            {
                RunId = runId,
                StepIndex = stepIndex,
                StepTarget = target,
                ExceptionMessage = message
            }
        }, ct);
    }

    private async Task EmitRunFinishedAsync(bool success, string? output, string? error, double durationMs, CancellationToken ct)
    {
        await EmitAsync(new RunFinishedEvent
        {
            Payload = new RunFinishedEvent.RunFinishedPayload
            {
                Success = success,
                FinalOutput = output,
                Error = error,
                TotalDurationMs = durationMs
            }
        }, ct);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static string ComputeHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static ExecutionResult Fail(string? error)
        => new() { Success = false, Error = error };
}
