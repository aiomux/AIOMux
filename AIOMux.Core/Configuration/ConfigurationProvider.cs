using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIOMux.Core.Configuration;

/// <summary>
/// Provides configuration loading and saving functionality
/// </summary>
public class ConfigurationProvider
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// The loaded application configuration
    /// </summary>
    public AppConfig Config { get; private set; } = new AppConfig();

    /// <summary>
    /// Path to the configuration file
    /// </summary>
    public string ConfigFilePath { get; }

    /// <summary>
    /// Creates a new instance of the ConfigurationProvider
    /// </summary>
    /// <param name="configFilePath">Path to the configuration file</param>
    public ConfigurationProvider(string configFilePath)
    {
        ConfigFilePath = configFilePath;
    }

    /// <summary>
    /// Loads the configuration from the specified file
    /// </summary>
    /// <returns>The loaded configuration</returns>
    public async Task<AppConfig> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                Console.WriteLine($"Configuration file not found at {ConfigFilePath}. Creating with default settings.");
                await SaveConfigAsync(Config);
                return Config;
            }

            var json = await File.ReadAllTextAsync(ConfigFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);

            if (config != null)
            {
                // Validate config
                if (!config.Validate(out var errors))
                {
                    Console.WriteLine($"Configuration validation failed:");
                    foreach (var error in errors)
                        Console.WriteLine($"  - {error}");
                }
                else
                {
                    Console.WriteLine($"Configuration loaded and validated from {ConfigFilePath}");
                }
                Config = config;
            }

            return Config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            return Config; // Return default config
        }
    }

    /// <summary>
    /// Saves the configuration to the specified file
    /// </summary>
    /// <param name="config">The configuration to save</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task SaveConfigAsync(AppConfig config)
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(ConfigFilePath, json);
            Console.WriteLine($"Configuration saved to {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates specific configuration values and saves the configuration
    /// </summary>
    /// <param name="updater">Action to update configuration values</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UpdateConfigAsync(Action<AppConfig> updater)
    {
        updater(Config);
        await SaveConfigAsync(Config);
    }
}