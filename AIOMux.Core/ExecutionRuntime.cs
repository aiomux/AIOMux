using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using AIOMux.Core.Policy;
using AIOMux.Core.Replay;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace AIOMux.Core;

/// <summary>
/// Canonical orchestration engine. Executes an <see cref="ExecutionPlan"/> step by step,
/// routing each step to its agent or tool, managing state, emitting events, and recording traces.
/// 
/// Plans are created via IExecutionPlanBuilder implementations (JsonExecutionPlanBuilder,
/// PlannerExecutionPlanBuilder, ReplayForkPlanBuilder) and passed to RunAsync or ForkAsync.
/// The runtime is agnostic to plan origin; it only cares about executing the plan's steps
/// and recording results in ExecutionRecord.
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
                    var stepResult = await ExecuteStepAsync(step, ctx, cancellationToken);
                    stepSw.Stop();

                    // Check if step was denied or failed
                    if (!stepResult.Success)
                    {
                        var msg = stepResult.Error ?? "Step execution failed";
                        _logger.LogWarning("Step {StepId} ({Target}) denied or failed: {Message}",
                            step.Id, step.Target, msg);

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

                        _logger.LogInformation("Plan execution halted at step {StepIndex} ({Target}): {Message}",
                            i, step.Target, msg);
                        return new ExecutionResult { Success = false, Error = msg, StepIndex = i, StepId = step.Id };
                    }

                    // Step succeeded
                    output = stepResult.Output;
                    var outputKey = step.OutputKey ?? step.Target;
                    ctx.State[outputKey] = output;

                    foreach (var (key, value) in stepResult.Outputs)
                        ctx.State[key] = value;

                    ctx.Records.Add(new ExecutionRecord
                    {
                        RunId = ctx.RunId,
                        StepId = step.Id,
                        StepIndex = i,
                        Type = step.Type,
                        Target = step.Target,
                        Input = ctx.GetInput(),
                        Output = output,
                        Success = true,
                        Timestamp = stepStart,
                        DurationMs = stepSw.Elapsed.TotalMilliseconds
                    });

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

            // Optionally append a summary report
            if (ctx.Options.GenerateJobSummary && ctx.Options.CollectMetrics)
                output = AppendSummary(output, ctx, plan.Name);

            sw.Stop();
            _logger.LogInformation("Plan '{PlanName}' completed in {Ms:F1}ms", plan.Name, sw.Elapsed.TotalMilliseconds);
            return new ExecutionResult { Success = true, Output = output };
        }
        catch (OperationCanceledException)
        {
            var error = "Execution was cancelled";
            _logger.LogWarning(error);
            return new ExecutionResult { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error during plan execution: {ex.Message}";
            _logger.LogError(ex, error);
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

    private async Task<StepExecutionResult> ExecuteStepAsync(
        ExecutionStep step,
        ExecutionContext ctx,
        CancellationToken ct)
    {
        // 1. Resolve all step inputs from static values and bindings
        var resolvedInputs = StepInputResolver.ResolveInputs(step, ctx);

        // 2. Evaluate policy for this step (applies to both tools and agents)
        var stepMetadata = new ExecutionStepMetadata
        {
            StepId = step.Id,
            Type = step.Type,
            Target = step.Target
        };

        var policyDecision = ctx.PolicyEngine.EvaluateStep(stepMetadata, resolvedInputs, ctx);

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

        // 3. Dispatch to appropriate executor
        if (step.Type == "tool")
        {
            return await ExecuteToolStepAsync(step, resolvedInputs, ctx, ct);
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

    // ── Tool execution ────────────────────────────────────────────────────────

    /// <summary>
    /// Orchestrates tool execution: executes tool, handles replay, and returns result.
    /// Policy is evaluated at the step level in ExecuteStepAsync before dispatch.
    /// Execution trace is recorded in ExecutionRecord, not event-based.
    /// </summary>
    private async Task<StepExecutionResult> ExecuteToolStepAsync(
        ExecutionStep step,
        ImmutableDictionary<string, object?> resolvedInputs,
        ExecutionContext ctx,
        CancellationToken ct)
    {
        // 1. Get primary input value for tool execution
        var input = StepInputResolver.GetInputString(resolvedInputs, "input");

        // 2. Generate deterministic call ID
        var stepIndex = ctx.State.TryGetValue("stepIndex", out var stepIndexValue)
            ? stepIndexValue?.ToString() ?? string.Empty
            : string.Empty;
        var callId = DeterministicCallId.Generate(ctx.RunId, stepIndex, step.Target, input);

        // 3. Check if tool exists
        if (!ctx.Tools.TryGetValue(step.Target, out var tool))
        {
            var msg = $"Tool not found: {step.Target}";
            return new StepExecutionResult
            {
                Success = false,
                Error = msg
            };
        }

        var call = new ToolCall { CallId = callId, ToolName = step.Target, JsonArgs = input };

        // 4. Check replay source
        ToolResult toolResult;

        if ((ctx.ReplayMode == ReplayMode.Full || ctx.ReplayMode == ReplayMode.ToolsOnly)
            && ctx.ReplaySource != null
            && ctx.ReplaySource.TryGetToolResult(call.CallId, out var replayedResult))
        {
            // Use recorded result from replay
            toolResult = replayedResult;
        }
        else
        {
            // 5. Execute tool
            ct.ThrowIfCancellationRequested();

            try
            {
                var output = await tool.ExecuteAsync(input);
                toolResult = new ToolResult
                {
                    CallId = call.CallId,
                    Success = true,
                    JsonResult = output
                };
            }
            catch (Exception ex)
            {
                toolResult = new ToolResult
                {
                    CallId = call.CallId,
                    Success = false,
                    Error = ex.Message,
                    JsonResult = string.Empty
                };
            }
        }

        // 6. Return step result
        if (!toolResult.Success)
            throw new InvalidOperationException(toolResult.Error ?? $"Tool '{step.Target}' failed");

        return new StepExecutionResult { Success = true, Output = toolResult.JsonResult };
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

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static ExecutionResult Fail(string? error)
        => new() { Success = false, Error = error };
}
