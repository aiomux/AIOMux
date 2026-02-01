namespace AIOMux.Local.Config;

/// <summary>
/// Root configuration for AIOMux local workspace.
/// </summary>
public class AiomuxConfig
{
    /// <summary>
    /// Default agent name to use when running commands
    /// </summary>
    public string DefaultAgentName { get; set; } = "assistant";

    /// <summary>
    /// Model configuration for LLM provider and connection
    /// </summary>
    public ModelConfig Model { get; set; } = new();

    /// <summary>
    /// Path to the skills directory (relative or absolute)
    /// </summary>
    public string SkillsPath { get; set; } = "./skills";

    /// <summary>
    /// Optional path to solutions directory
    /// </summary>
    public string? SolutionsPath { get; set; }

    /// <summary>
    /// Permission settings for capabilities: deny|confirm|allow
    /// </summary>
    public Dictionary<string, string> Permissions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        { "shell.exec", "deny" },
        { "fs.write", "deny" }
    };

    /// <summary>
    /// Shell execution configuration
    /// </summary>
    public ShellConfig Shell { get; set; } = new();
}
