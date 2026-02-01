using System.Text.Json;

namespace AIOMux.Local.Config;

/// <summary>
/// Loads and validates AIOMux configuration from aiomux.json
/// </summary>
public class ConfigLoader
{
    private const string ConfigFileName = "aiomux.json";

    /// <summary>
    /// Loads configuration from the specified directory or current directory if not found.
    /// </summary>
    public static AiomuxConfig Load(string? basePath = null)
    {
        basePath ??= Directory.GetCurrentDirectory();
        var configPath = Path.Combine(basePath, ConfigFileName);

        if (!File.Exists(configPath))
        {
            Console.Error.WriteLine($"Error: {ConfigFileName} not found at {configPath}");
            Console.Error.WriteLine("Run 'aiomux init' first to create a workspace.");
            throw new FileNotFoundException($"Configuration file not found: {configPath}");
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<AiomuxConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (config == null)
            {
                throw new InvalidOperationException("Failed to deserialize configuration");
            }

            ValidateConfig(config);
            return config;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Error: Invalid JSON in {ConfigFileName}");
            Console.Error.WriteLine($"Details: {ex.Message}");
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error loading {ConfigFileName}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Validates the configuration and throws if required fields are missing or invalid.
    /// </summary>
    private static void ValidateConfig(AiomuxConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.DefaultAgentName))
        {
            throw new InvalidOperationException("DefaultAgentName cannot be empty");
        }

        if (config.Model == null)
        {
            throw new InvalidOperationException("Model configuration is required");
        }

        if (string.IsNullOrWhiteSpace(config.Model.Provider))
        {
            throw new InvalidOperationException("Model.Provider must be specified (ollama|openai)");
        }

        if (!new[] { "ollama", "openai" }.Contains(config.Model.Provider, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported model provider: {config.Model.Provider}");
        }

        if (string.IsNullOrWhiteSpace(config.Model.ModelName))
        {
            throw new InvalidOperationException("Model.ModelName cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(config.SkillsPath))
        {
            throw new InvalidOperationException("SkillsPath cannot be empty");
        }
    }

    /// <summary>
    /// Creates and saves a default configuration to the specified directory.
    /// </summary>
    public static void CreateDefault(string basePath)
    {
        var config = new AiomuxConfig();
        var configPath = Path.Combine(basePath, ConfigFileName);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(configPath, json);
    }
}
