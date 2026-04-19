using System.Text.Json;

namespace AIOMux.Local;

/// <summary>
/// Loads and validates a solution definition from a solution.json file.
/// Resolves relative paths to absolute paths based on the solution directory.
/// </summary>
public class SolutionLoader
{
    private readonly string _solutionPath;
    private readonly string _solutionDirectory;

    /// <summary>
    /// Creates a new solution loader for the given solution.json path.
    /// </summary>
    /// <param name="solutionJsonPath">Full path to solution.json</param>
    /// <exception cref="FileNotFoundException">If solution.json does not exist</exception>
    public SolutionLoader(string solutionJsonPath)
    {
        if (!File.Exists(solutionJsonPath))
            throw new FileNotFoundException($"Solution file not found: {solutionJsonPath}");

        _solutionPath = Path.GetFullPath(solutionJsonPath);
        _solutionDirectory = Path.GetDirectoryName(_solutionPath)
            ?? throw new InvalidOperationException($"Could not determine directory for {solutionJsonPath}");
    }

    /// <summary>
    /// Loads the solution definition from the solution.json file.
    /// </summary>
    /// <returns>Loaded and validated SolutionDefinition</returns>
    public SolutionDefinition Load()
    {
        try
        {
            var json = File.ReadAllText(_solutionPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var definition = JsonSerializer.Deserialize<SolutionDefinition>(json, options)
                ?? throw new InvalidOperationException("Failed to deserialize solution definition");

            // Validate required configuration fields.
            ValidateDefinition(definition);

            // Resolve relative paths to absolute paths.
            ResolvePaths(definition);

            return definition;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Invalid JSON in solution.json: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates the solution definition for required fields.
    /// </summary>
    private void ValidateDefinition(SolutionDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
            throw new InvalidOperationException("Solution must have a name");

        if (string.IsNullOrWhiteSpace(definition.Entry))
            throw new InvalidOperationException("Solution must have an entry point");

        if (string.IsNullOrWhiteSpace(definition.PolicyConfig))
            throw new InvalidOperationException("Solution must specify a policyConfig path");
    }

    /// <summary>
    /// Resolves relative paths in the solution definition to absolute paths.
    /// </summary>
    private void ResolvePaths(SolutionDefinition definition)
    {
        // Set the working directory when it is not specified.
        if (string.IsNullOrWhiteSpace(definition.WorkingDirectory))
            definition.WorkingDirectory = _solutionDirectory;
        else if (!Path.IsPathRooted(definition.WorkingDirectory))
            definition.WorkingDirectory = Path.Combine(_solutionDirectory, definition.WorkingDirectory);

        // Resolve the entry path.
        if (!Path.IsPathRooted(definition.Entry))
            definition.Entry = Path.Combine(_solutionDirectory, definition.Entry);

        // Resolve the policy configuration path.
        if (!string.IsNullOrWhiteSpace(definition.PolicyConfig) && !Path.IsPathRooted(definition.PolicyConfig))
            definition.PolicyConfig = Path.Combine(_solutionDirectory, definition.PolicyConfig);

        // Resolve the memory configuration path.
        if (!string.IsNullOrWhiteSpace(definition.MemoryConfig) && !Path.IsPathRooted(definition.MemoryConfig))
            definition.MemoryConfig = Path.Combine(_solutionDirectory, definition.MemoryConfig);

        // Resolve the replay storage path.
        if (definition.Replay != null && !string.IsNullOrWhiteSpace(definition.Replay.StoragePath))
        {
            if (!Path.IsPathRooted(definition.Replay.StoragePath))
                definition.Replay.StoragePath = Path.Combine(_solutionDirectory, definition.Replay.StoragePath);
        }

        // Resolve assembly paths.
        for (int i = 0; i < definition.Assemblies.Count; i++)
        {
            if (!Path.IsPathRooted(definition.Assemblies[i]))
                definition.Assemblies[i] = Path.Combine(_solutionDirectory, definition.Assemblies[i]);
        }
    }

    /// <summary>
    /// Validates that all referenced files exist.
    /// </summary>
    public void ValidateReferences(SolutionDefinition definition)
    {
        if (!File.Exists(definition.Entry))
            throw new FileNotFoundException($"Entry point file not found: {definition.Entry}");

        if (!File.Exists(definition.PolicyConfig!))
            throw new FileNotFoundException($"Policy config file not found: {definition.PolicyConfig}");

        if (!string.IsNullOrWhiteSpace(definition.MemoryConfig) && !File.Exists(definition.MemoryConfig))
            throw new FileNotFoundException($"Memory config file not found: {definition.MemoryConfig}");

        foreach (var assemblyPath in definition.Assemblies)
        {
            if (!File.Exists(assemblyPath))
                throw new FileNotFoundException($"Assembly not found: {assemblyPath}");
        }
    }
}
