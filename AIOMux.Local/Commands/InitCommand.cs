using AIOMux.Local.Config;

namespace AIOMux.Local.Commands;

/// <summary>
/// Handles the 'init' command to create a new workspace.
/// </summary>
public class InitCommand
{
    /// <summary>
    /// Executes workspace initialization.
    /// </summary>
    public static void Execute(string? targetPath = null)
    {
        var basePath = targetPath ?? Directory.GetCurrentDirectory();

        // Check if already initialized
        var configPath = Path.Combine(basePath, "aiomux.json");
        if (File.Exists(configPath))
        {
            Console.WriteLine($"? Workspace already initialized at {basePath}");
            return;
        }

        Console.WriteLine($"?? Initializing AIOMux workspace at {basePath}");

        try
        {
            // Create directories
            var skillsDir = Path.Combine(basePath, "skills");
            var sandboxDir = Path.Combine(basePath, "sandbox");

            Directory.CreateDirectory(skillsDir);
            Directory.CreateDirectory(sandboxDir);

            // Create default config
            ConfigLoader.CreateDefault(basePath);

            Console.WriteLine();
            Console.WriteLine("? Workspace initialized successfully!");
            Console.WriteLine();
            Console.WriteLine("?? Created directories:");
            Console.WriteLine($"   • {skillsDir}");
            Console.WriteLine($"   • {sandboxDir}");
            Console.WriteLine();
            Console.WriteLine("?? Created files:");
            Console.WriteLine($"   • {configPath}");
            Console.WriteLine();
            Console.WriteLine("?? Next steps:");
            Console.WriteLine("   1. Add skill plugins to the skills/ directory");
            Console.WriteLine("   2. Run 'aiomux run' to start the REPL");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"? Failed to initialize workspace: {ex.Message}");
            throw;
        }
    }
}
