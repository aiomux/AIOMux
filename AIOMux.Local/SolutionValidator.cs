using AIOMux.Connectors;
using AIOMux.Core;
using AIOMux.Core.Builders;
using AIOMux.Core.Interfaces;
using System.Reflection;

namespace AIOMux.Local;

/// <summary>
/// Performs a minimal validation pass for demo-critical solution setup.
/// </summary>
public sealed class SolutionValidator
{
    public async Task ValidateAsync(string solutionJsonPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(solutionJsonPath))
            throw new InvalidOperationException($"Solution file does not exist: '{solutionJsonPath}'.");

        SolutionDefinition solution;
        try
        {
            var loader = new SolutionLoader(solutionJsonPath);
            solution = loader.Load();
            loader.ValidateReferences(solution);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid solution setup: {ex.Message}", ex);
        }

        ValidateUniqueConnectorNames(solution);
        ValidateConnectorTypes(solution);

        var plan = await ValidateAndBuildPlanAsync(solution, cancellationToken);
        ValidateReferencedAgents(solution, plan);
    }

    private static void ValidateUniqueConnectorNames(SolutionDefinition solution)
    {
        var duplicates = solution.Connectors
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key) && g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Connector names must be unique. Duplicates: {string.Join(", ", duplicates)}.");
        }
    }

    private static void ValidateConnectorTypes(SolutionDefinition solution)
    {
        foreach (var connector in solution.Connectors)
        {
            try
            {
                _ = BuiltInConnectorRegistry.ResolveType(connector.Type);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Connector '{connector.Name}' declares unknown type '{connector.Type}': {ex.Message}", ex);
            }
        }
    }

    private static async Task<AIOMux.Core.Models.ExecutionPlan> ValidateAndBuildPlanAsync(
        SolutionDefinition solution,
        CancellationToken cancellationToken)
    {
        var planPath = solution.Entry;
        if (!File.Exists(planPath))
            throw new InvalidOperationException($"Plan file does not exist: '{planPath}'.");

        var planJson = await File.ReadAllTextAsync(planPath, cancellationToken);

        try
        {
            var builder = new JsonExecutionPlanBuilder(planJson, planName: solution.Name);
            return await builder.BuildAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Invalid plan JSON '{planPath}': {ex.Message}", ex);
        }
    }

    private static void ValidateReferencedAgents(
        SolutionDefinition solution,
        AIOMux.Core.Models.ExecutionPlan plan)
    {
        var agentNames = plan.Steps
            .Where(s => string.Equals(s.Type, "agent", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Target)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(solution.EntryAgent))
            agentNames.Add(solution.EntryAgent);

        var extensionAgentNames = DiscoverExtensionAgentNames(solution.Assemblies);

        foreach (var agentName in agentNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (extensionAgentNames.Contains(agentName))
                continue;

            try
            {
                _ = BuiltInAgentRegistry.ResolveType(agentName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Agent '{agentName}' is referenced by the plan but is not available as a built-in or extension agent: {ex.Message}", ex);
            }
        }
    }

    private static HashSet<string> DiscoverExtensionAgentNames(IEnumerable<string> assemblyPaths)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var assemblyPath in assemblyPaths)
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(assemblyPath);
            }
            catch
            {
                continue;
            }

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(IAgent).IsAssignableFrom(t)))
            {
                try
                {
                    if (Activator.CreateInstance(type) is IAgent agent && !string.IsNullOrWhiteSpace(agent.Name))
                        names.Add(agent.Name);
                }
                catch
                {
                    // Ignore non-instantiable agent types during validation discovery.
                }
            }
        }

        return names;
    }
}
