using AIOMux.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIOMux.Core;

/// <summary>
/// Orchestrates the execution of agent chains with comprehensive validation, error handling, and logging.
/// </summary>
public class AgentOrchestrator
{
    private readonly AgentManager _agentManager;
    private readonly ILogger<AgentOrchestrator> _logger;

    /// <summary>
    /// Creates a new instance of the agent orchestrator.
    /// </summary>
    /// <param name="agentManager">The agent manager to use for managing agents.</param>
    /// <param name="logger">The logger to use for logging operations.</param>
    public AgentOrchestrator(AgentManager agentManager, ILogger<AgentOrchestrator>? logger = null)
    {
        _agentManager = agentManager ?? throw new ArgumentNullException(nameof(agentManager));
        _logger = logger ?? NullLogger<AgentOrchestrator>.Instance;
    }

    /// <summary>
    /// Executes a chain of agents with the given context.
    /// </summary>
    /// <param name="chainModel">The chain model to execute.</param>
    /// <param name="context">The context for execution.</param>
    /// <returns>The final output from chain execution.</returns>
    public Task<string> ExecuteChainAsync(AgentChainModel chainModel, AgentContext context)
    {
        return ExecuteChainAsync(chainModel, context, true);
    }

    /// <summary>
    /// Executes a chain of agents with the given context and optional metrics collection.
    /// </summary>
    /// <param name="chainModel">The chain model to execute.</param>
    /// <param name="context">The context for execution.</param>
    /// <param name="generateSummary">Whether to generate a job summary.</param>
    /// <returns>The final output from chain execution or job summary based on parameters.</returns>
    public async Task<string> ExecuteChainAsync(AgentChainModel chainModel, AgentContext context, bool generateSummary)
    {
        // Validate inputs
        if (chainModel == null)
        {
            var errorMessage = "Chain model cannot be null";
            _logger.LogError(errorMessage);
            throw new ArgumentNullException(nameof(chainModel));
        }

        if (context == null)
        {
            var errorMessage = "Context cannot be null";
            _logger.LogError(errorMessage);
            throw new ArgumentNullException(nameof(context));
        }

        // Validate chain model
        if (!chainModel.Validate(out var errors))
        {
            var errorMessage = $"Chain validation failed: {string.Join(", ", errors)}";
            _logger.LogError("Chain validation failed for '{ChainName}': {Errors}", chainModel.Name, string.Join(", ", errors));
            return errorMessage;
        }

        _logger.LogInformation("Starting execution of agent chain: {ChainName} with {StepCount} steps",
            chainModel.Name, chainModel.Steps.Count);

        var startTime = DateTime.UtcNow;
        var allMetrics = new List<AgentMetrics>();
        string result = string.Empty;

        try
        {
            // Validate all agents exist before execution
            var missingAgents = new List<string>();
            foreach (var step in chainModel.Steps)
            {
                var agent = _agentManager.GetByName(step.AgentName);
                if (agent == null)
                {
                    missingAgents.Add(step.AgentName);
                }
            }

            if (missingAgents.Any())
            {
                var errorMessage = $"Agents not found: {string.Join(", ", missingAgents)}";
                _logger.LogError("Missing agents in chain '{ChainName}': {MissingAgents}",
                    chainModel.Name, string.Join(", ", missingAgents));
                return errorMessage;
            }

            // Execute each step in the chain
            for (int i = 0; i < chainModel.Steps.Count; i++)
            {
                var step = chainModel.Steps[i];
                var agent = _agentManager.GetByName(step.AgentName)!; // Safe due to validation above

                try
                {
                    _logger.LogInformation("Executing step {StepNumber}/{TotalSteps}: {AgentName}",
                        i + 1, chainModel.Steps.Count, step.AgentName);

                    // Set the input for this step if specified
                    await SetStepInputAsync(step, context);

                    // Execute the agent with or without metrics
                    if (context.Options.CollectMetrics)
                    {
                        var (stepResult, metrics) = await agent.ExecuteWithMetricsAsync(context);
                        result = stepResult;

                        if (metrics != null)
                        {
                            allMetrics.Add(metrics);
                            _logger.LogDebug("Agent {AgentName} completed in {ExecutionTime}ms",
                                step.AgentName, metrics.ExecutionTimeMs);
                        }
                    }
                    else
                    {
                        result = await agent.ExecuteAsync(context);
                    }

                    // Store the result in context variables
                    var outputKey = step.OutputTo ?? step.AgentName;
                    context.Variables[outputKey] = result;

                    _logger.LogDebug("Agent {AgentName} completed successfully, output stored as {OutputKey}",
                        step.AgentName, outputKey);
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Error executing agent {step.AgentName} in step {i + 1}: {ex.Message}";
                    _logger.LogError(ex, "Error executing agent {AgentName} in step {StepNumber}: {Error}",
                        step.AgentName, i + 1, ex.Message);
                    return errorMessage;
                }
            }

            var endTime = DateTime.UtcNow;
            var totalExecutionTime = (endTime - startTime).TotalMilliseconds;

            _logger.LogInformation("Chain execution completed successfully: {ChainName} in {ExecutionTime}ms",
                chainModel.Name, totalExecutionTime);

            // Generate and store job summary if requested
            if (context.Options.GenerateJobSummary && context.Options.CollectMetrics)
            {
                var jobSummary = new JobSummary
                {
                    Name = chainModel.Name,
                    StartTime = startTime,
                    EndTime = endTime,
                    TotalExecutionTimeMs = totalExecutionTime,
                    AgentCount = chainModel.Steps.Count,
                    AgentMetrics = allMetrics
                };

                context.Variables["JobSummary"] = jobSummary;

                // Return summary if requested
                if (generateSummary && context.Options.GenerateJobSummary)
                {
                    var summaryReport = jobSummary.CreateReport(context.Options.IncludeDetailedMetrics);
                    return $"{result}\n\n{summaryReport}";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error during chain execution: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during chain execution for '{ChainName}': {Error}",
                chainModel.Name, ex.Message);
            return errorMessage;
        }
    }

    /// <summary>
    /// Sets the input for a step based on the InputFrom property.
    /// </summary>
    /// <param name="step">The agent step to set input for.</param>
    /// <param name="context">The context containing variables and user input.</param>
    private async Task SetStepInputAsync(AgentStep step, AgentContext context)
    {
        if (string.IsNullOrEmpty(step.InputFrom))
        {
            return;
        }

        try
        {
            // If InputFrom is "user", use the original user input
            if (step.InputFrom == "user")
            {
                context.UserInput = context.Variables.ContainsKey("user")
                    ? context.Variables["user"]?.ToString() ?? context.UserInput
                    : context.UserInput;

                _logger.LogDebug("Using user input for agent {AgentName}", step.AgentName);
            }
            // Otherwise, use the value from context.Variables (could be OutputTo from a previous step)
            else if (context.Variables.ContainsKey(step.InputFrom))
            {
                context.UserInput = context.Variables[step.InputFrom]?.ToString() ?? string.Empty;
                _logger.LogDebug("Using input from {InputSource} for agent {AgentName}",
                    step.InputFrom, step.AgentName);
            }
            else
            {
                _logger.LogWarning("InputFrom '{InputFrom}' not found in context variables for agent {AgentName}",
                    step.InputFrom, step.AgentName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting input for agent {AgentName}: {Error}",
                step.AgentName, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Loads and executes a chain from a JSON file.
    /// </summary>
    /// <param name="filePath">The path to the JSON file containing the chain definition.</param>
    /// <param name="context">The context for execution.</param>
    /// <returns>The result of executing the chain.</returns>
    public async Task<string> ExecuteChainFromFileAsync(string filePath, AgentContext context)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        _logger.LogInformation("Loading chain from file: {FilePath}", filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                var errorMessage = $"Chain file not found: {filePath}";
                _logger.LogError(errorMessage);
                return errorMessage;
            }

            var chainModel = await AgentChainModel.LoadFromFileAsync(filePath);
            return await ExecuteChainAsync(chainModel, context);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to load or execute chain from file: {ex.Message}";
            _logger.LogError(ex, "Failed to load or execute chain from file '{FilePath}': {Error}",
                filePath, ex.Message);
            return errorMessage;
        }
    }

    /// <summary>
    /// Loads and executes a chain from a JSON file with optional summary generation.
    /// </summary>
    /// <param name="filePath">The path to the JSON file containing the chain definition.</param>
    /// <param name="context">The context for execution.</param>
    /// <param name="generateSummary">Whether to generate a summary of the execution.</param>
    /// <returns>The result of executing the chain.</returns>
    public async Task<string> ExecuteChainFromFileAsync(string filePath, AgentContext context, bool generateSummary)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        _logger.LogInformation("Loading chain from file: {FilePath}", filePath);

        try
        {
            if (!File.Exists(filePath))
            {
                var errorMessage = $"Chain file not found: {filePath}";
                _logger.LogError(errorMessage);
                return errorMessage;
            }

            var chainModel = await AgentChainModel.LoadFromFileAsync(filePath);
            return await ExecuteChainAsync(chainModel, context, generateSummary);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to load or execute chain from file: {ex.Message}";
            _logger.LogError(ex, "Failed to load or execute chain from file '{FilePath}': {Error}",
                filePath, ex.Message);
            return errorMessage;
        }
    }

    /// <summary>
    /// Dynamically creates and executes a chain using a planner agent.
    /// </summary>
    /// <param name="plannerAgentName">The name of the planner agent to use.</param>
    /// <param name="userInput">The user input to provide to the planner.</param>
    /// <param name="context">Optional custom context (will be created if null).</param>
    /// <returns>The result of executing the dynamic chain.</returns>
    public async Task<string> ExecuteDynamicChainAsync(string plannerAgentName, string userInput, AgentContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(plannerAgentName))
        {
            throw new ArgumentException("Planner agent name cannot be null or empty", nameof(plannerAgentName));
        }

        if (string.IsNullOrWhiteSpace(userInput))
        {
            throw new ArgumentException("User input cannot be null or empty", nameof(userInput));
        }

        context ??= new AgentContext();
        context.UserInput = userInput;

        _logger.LogInformation("Generating dynamic chain using planner agent: {PlannerAgent}", plannerAgentName);

        try
        {
            // Get the planner agent
            var plannerAgent = _agentManager.GetByName(plannerAgentName);
            if (plannerAgent == null)
            {
                var errorMessage = $"Planner agent not found: {plannerAgentName}";
                _logger.LogError("Planner agent not found: {PlannerAgent}", plannerAgentName);
                return errorMessage;
            }

            // Add available agents to the context for the planner to use
            context.Variables["AvailableAgents"] = _agentManager.GetFormattedAgentList();

            // Execute the planner to get the chain JSON
            var planJson = await plannerAgent.ExecuteAsync(context);
            _logger.LogDebug("Planner generated chain: {PlanJson}", planJson);

            // Parse the JSON into a chain model
            var chainModel = DynamicChainModel.Parse(
                planJson,
                $"DynamicChain-{DateTime.UtcNow:yyyyMMddHHmmss}"
            );

            // Store the original plan in the context
            context.Variables["DynamicPlan"] = planJson;

            // Execute the generated chain
            _logger.LogInformation("Executing dynamic chain with {StepCount} steps", chainModel.Steps.Count);
            return await ExecuteChainAsync(chainModel, context);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to execute dynamic chain: {ex.Message}";
            _logger.LogError(ex, "Failed to execute dynamic chain with planner '{PlannerAgent}': {Error}",
                plannerAgentName, ex.Message);
            return errorMessage;
        }
    }

    /// <summary>
    /// Executes the orchestrator with a given context, automatically using the planner agent
    /// to create and execute a dynamic chain.
    /// </summary>
    /// <param name="context">The agent context containing the user input.</param>
    /// <returns>The result of executing the dynamic chain.</returns>
    public Task<string> ExecuteAsync(AgentContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        // Use the PlannerAgent to create and execute a dynamic chain
        return ExecuteDynamicChainAsync("PlannerAgent", context.UserInput, context);
    }
}