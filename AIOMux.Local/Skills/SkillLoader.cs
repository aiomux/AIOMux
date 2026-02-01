namespace AIOMux.Local.Skills;

/// <summary>
/// Loads and discovers skills/plugins from the skills directory.
/// </summary>
public class SkillLoader
{
    private readonly string _skillsPath;

    public SkillLoader(string skillsPath)
    {
        _skillsPath = skillsPath;
    }

    /// <summary>
    /// Discovers plugin DLL files in the skills directory.
    /// Returns paths to all .dll files that could be plugins.
    /// </summary>
    public IEnumerable<string> DiscoverPlugins()
    {
        if (!Directory.Exists(_skillsPath))
        {
            return [];
        }

        try
        {
            return Directory.GetFiles(_skillsPath, "*.dll", SearchOption.AllDirectories);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to discover plugins in {_skillsPath}: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Checks if a skill folder has a manifest.json file.
    /// </summary>
    public bool HasManifest(string skillFolderPath)
    {
        var manifestPath = Path.Combine(skillFolderPath, "manifest.json");
        return File.Exists(manifestPath);
    }

    /// <summary>
    /// Lists all subdirectories in the skills path (skill folders).
    /// </summary>
    public IEnumerable<string> GetSkillFolders()
    {
        if (!Directory.Exists(_skillsPath))
        {
            return [];
        }

        try
        {
            return Directory.GetDirectories(_skillsPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: Failed to list skill folders: {ex.Message}");
            return [];
        }
    }
}
