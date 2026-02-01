namespace AIOMux.Local.Config;

/// <summary>
/// Shell execution configuration with security settings.
/// </summary>
public class ShellConfig
{
    /// <summary>
    /// Root directory where shell commands are sandboxed
    /// </summary>
    public string SandboxRoot { get; set; } = "./sandbox";

    /// <summary>
    /// List of executable names that are allowed to run (empty = all allowed when permission is "allow")
    /// </summary>
    public List<string> AllowedExecutables { get; set; } = [];

    /// <summary>
    /// Command execution timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum output size in bytes
    /// </summary>
    public int MaxOutputBytes { get; set; } = 1_000_000;

    /// <summary>
    /// Maximum commands allowed per minute
    /// </summary>
    public int MaxCommandsPerMinute { get; set; } = 60;
}
