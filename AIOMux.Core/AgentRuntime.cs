using AIOMux.Core.Interfaces;
using AIOMux.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIOMux.Core;

/// <summary>
/// Implementation of IAgentRuntime that provides a stable, host-friendly API
/// for executing agents or chains without exposing internal orchestration details.
/// </summary>
public class AgentRuntime : IAgentRuntime
{
    private readonly IAgentManager _agentManager;
    private readonly AgentOrchestrator _orchestrator;
    private readonly ILogger<AgentRuntime> _logger;

    /// <summary>
    /// Creates a new instance of the agent runtime facade.
    /// </summary>
    /// <param name="agentManager">The agent manager for agent/chain lookup.</param>
    /// <param name="orchestrator">The orchestrator for chain execution.</param>
    /// <param name="logger">Optional logger for runtime operations.</param>
    public AgentRuntime(IAgentManager agentManager, AgentOrchestrator orchestrator, ILogger<AgentRuntime>? logger = null)
    {
        _agentManager = agentManager ?? throw new ArgumentNullException(nameof(agentManager));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? NullLogger<AgentRuntime>.Instance;
    }

    /// <summary>
    /// Runs an agent or chain with the given request and optional cancellation.
    /// </summary>
    /// <param name="request">The execution request specifying which agent or chain to run.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Structured result with success status, output, and optional error details.</returns>
    public async Task<AgentRuntimeResult> RunAsync(AgentRunRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (request == null)
            {
                var error = "Request cannot be null";
                _logger.LogError(error);
                return new AgentRuntimeResult { Success = false, Error = error };
            }

            if (request.Context == null)
            {
                var error = "Request context cannot be null";
                _logger.LogError(error);
                return new AgentRuntimeResult { Success = false, Error = error };
            }

            // Exactly one of AgentName or ChainName must be specified
            var agentNameSet = !string.IsNullOrWhiteSpace(request.AgentName);
            var chainNameSet = !string.IsNullOrWhiteSpace(request.ChainName);

            if (!agentNameSet && !chainNameSet)
            {
                var error = "Either AgentName or ChainName must be specified";
                _logger.LogError(error);
                return new AgentRuntimeResult { Success = false, Error = error };
            }

            if (agentNameSet && chainNameSet)
            {
                var error = "Only one of AgentName or ChainName can be specified, not both";
                _logger.LogError(error);
                return new AgentRuntimeResult { Success = false, Error = error };
            }

            _logger.LogInformation("Starting runtime execution: AgentName={AgentName}, ChainName={ChainName}",
                request.AgentName ?? "null", request.ChainName ?? "null");

            if (agentNameSet)
            {
                return await ExecuteAgentAsync(request.AgentName!, request.Context, cancellationToken);
            }
            else
            {
                return await ExecuteChainAsync(request.ChainName!, request.Context, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            var error = "Execution was cancelled";
            _logger.LogWarning(error);
            return new AgentRuntimeResult { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            var error = $"Unexpected error in runtime execution: {ex.Message}";
            _logger.LogError(ex, error);
            return new AgentRuntimeResult { Success = false, Error = error };
        }
    }

    /// <summary>
    /// Executes a single agent with the given context.
    /// </summary>
    private async Task<AgentRuntimeResult> ExecuteAgentAsync(string agentName, AgentContext context, CancellationToken cancellationToken)
    {
        try
        {
            var agent = _agentManager.GetByName(agentName);
            if (agent == null)
            {
                var error = $"Agent not found: {agentName}";
                _logger.LogError(error);
                return new AgentRuntimeResult { Success = false, Error = error, AgentName = agentName };
            }

            _logger.LogInformation("Executing agent: {AgentName}", agentName);

            // Preserve original user input for replay friendliness
            if (!context.Variables.ContainsKey("user.input.original"))
            {
                context.Variables["user.input.original"] = context.UserInput;
            }

            string output;

            // Check if agent supports cancellation
            if (agent is ICancellableAgent cancellableAgent)
            {
                output = await cancellableAgent.ExecuteAsync(context, cancellationToken);
            }
            else
            {
                // For non-cancellable agents, we still respect the token by checking before/after
                cancellationToken.ThrowIfCancellationRequested();
                output = await agent.ExecuteAsync(context);
                cancellationToken.ThrowIfCancellationRequested();
            }

            _logger.LogInformation("Agent execution completed successfully: {AgentName}", agentName);
            return new AgentRuntimeResult { Success = true, Output = output, AgentName = agentName };
        }
        catch (OperationCanceledException)
        {
            var error = $"Agent execution was cancelled: {agentName}";
            _logger.LogWarning(error);
            return new AgentRuntimeResult { Success = false, Error = error, AgentName = agentName };
        }
        catch (Exception ex)
        {
            var error = $"Error executing agent {agentName}: {ex.Message}";
            _logger.LogError(ex, error);
            return new AgentRuntimeResult { Success = false, Error = error, AgentName = agentName };
        }
    }

    /// <summary>
    /// Executes a chain by name with the given context.
    /// </summary>
    private async Task<AgentRuntimeResult> ExecuteChainAsync(string chainName, AgentContext context, CancellationToken cancellationToken)
    {
        try
        {
            var chain = _agentManager.GetByName(chainName);
            if (chain == null)
            {
                var error = $"Chain not found: {chainName}";
                _logger.LogError(error);
                return new AgentRuntimeResult { Success = false, Error = error, AgentName = chainName };
            }

            // If chain is actually just a wrapped agent (AgentChain implements IAgent)
            // we can execute it directly, but we prefer using the orchestrator for chains
            // that are AgentChainModel. Check if we have a chain model in variables.

            var chainModel = context.Variables.ContainsKey("chain")
                ? context.Variables["chain"] as AgentChainModel
                : null;

            if (chainModel != null)
            {
                _logger.LogInformation("Executing chain model: {ChainName}", chainName);
                var result = await _orchestrator.ExecuteChainResultAsync(chainModel, context, cancellationToken);

                return new AgentRuntimeResult
                {
                    Success = result.Success,
                    Output = result.Output,
                    Error = result.Error,
                    StepIndex = result.StepIndex,
                    AgentName = result.AgentName
                };
            }
            else
            {
                // Fall back to executing the chain as an agent (if it's an AgentChain)
                _logger.LogInformation("Executing chain as agent: {ChainName}", chainName);

                // Preserve original user input for replay friendliness
                if (!context.Variables.ContainsKey("user.input.original"))
                {
                    context.Variables["user.input.original"] = context.UserInput;
                }

                string output;

                // Check if chain/agent supports cancellation
                if (chain is ICancellableAgent cancellableAgent)
                {
                    output = await cancellableAgent.ExecuteAsync(context, cancellationToken);
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    output = await chain.ExecuteAsync(context);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                _logger.LogInformation("Chain execution completed successfully: {ChainName}", chainName);
                return new AgentRuntimeResult { Success = true, Output = output, AgentName = chainName };
            }
        }
        catch (OperationCanceledException)
        {
            var error = $"Chain execution was cancelled: {chainName}";
            _logger.LogWarning(error);
            return new AgentRuntimeResult { Success = false, Error = error, AgentName = chainName };
        }
        catch (Exception ex)
        {
            var error = $"Error executing chain {chainName}: {ex.Message}";
            _logger.LogError(ex, error);
            return new AgentRuntimeResult { Success = false, Error = error, AgentName = chainName };
        }
    }
}
