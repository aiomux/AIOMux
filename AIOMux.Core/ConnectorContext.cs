using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;

namespace AIOMux.Core;

/// <summary>
/// Bridges a connector into the execution runtime.
/// Maps each <see cref="ConnectorEvent"/> to a standard plan + context pair and
/// dispatches it through the existing <see cref="IExecutionRuntime"/> without
/// creating a separate execution path.
/// </summary>
public sealed class ConnectorContext : IConnectorContext
{
    private readonly IExecutionRuntime _runtime;
    private readonly ExecutionPlan _plan;
    private readonly ExecutionRuntimeServices _services;
    private readonly string _defaultEntryAgent;
    private readonly Action<ConnectorEvent, ExecutionResult>? _onExecutionCompleted;

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Config { get; }

    /// <param name="runtime">The execution runtime to dispatch events through.</param>
    /// <param name="plan">The pre-loaded execution plan for this solution.</param>
    /// <param name="services">Runtime services (tools, agents, policy) already configured for this solution.</param>
    /// <param name="entryAgent">
    /// Optional default entry agent for connector events.
    /// When provided, takes precedence over the agent inferred from the first agent step in the plan.
    /// </param>
    /// <param name="config">Connector-specific configuration from the solution manifest.</param>
    /// <param name="onExecutionCompleted">Optional callback invoked after runtime execution for a published event.</param>
    public ConnectorContext(
        IExecutionRuntime runtime,
        ExecutionPlan plan,
        ExecutionRuntimeServices services,
        string? entryAgent = null,
        IReadOnlyDictionary<string, string>? config = null,
        Action<ConnectorEvent, ExecutionResult>? onExecutionCompleted = null)
    {
        _runtime = runtime;
        _plan = plan;
        _services = services;
        _defaultEntryAgent = !string.IsNullOrWhiteSpace(entryAgent)
            ? entryAgent
            : plan.Steps.FirstOrDefault(s => s.Type == "agent")?.Target ?? string.Empty;
        Config = config ?? new Dictionary<string, string>();
        _onExecutionCompleted = onExecutionCompleted;
    }

    /// <summary>
    /// Maps the connector event to a plan and execution context, then dispatches
    /// through the runtime using the standard agent execution path.
    /// Uses the solution's entry agent unless <see cref="ConnectorEvent.EntryAgent"/> is explicitly set.
    /// </summary>
    public async Task PublishAsync(ConnectorEvent evt, CancellationToken cancellationToken = default)
    {
        var entryAgent = string.IsNullOrWhiteSpace(evt.EntryAgent)
            ? _defaultEntryAgent
            : evt.EntryAgent;

        var plan = string.Equals(entryAgent, _defaultEntryAgent, StringComparison.OrdinalIgnoreCase)
            ? _plan
            : BuildSingleAgentPlan(entryAgent);

        var ctx = new ExecutionContext { Services = _services };
        ctx.Inputs["input"] = evt.Payload?.ToString() ?? string.Empty;
        ctx.State["connector.name"] = evt.ConnectorName;
        ctx.State["connector.eventType"] = evt.EventType;

        foreach (var (key, value) in evt.Metadata)
            ctx.State[key] = value;

        var result = await _runtime.ExecuteAsync(plan, ctx, cancellationToken);
        _onExecutionCompleted?.Invoke(evt, result);
    }

    private static ExecutionPlan BuildSingleAgentPlan(string agentName) => new()
    {
        Name = $"connector-{agentName}",
        Steps = [new ExecutionStep { Id = "connector-entry", Type = "agent", Target = agentName }]
    };
}
