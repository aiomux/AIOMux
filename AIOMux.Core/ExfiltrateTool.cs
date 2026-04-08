using AIOMux.Core.Interfaces;

namespace AIOMux.Core;

/// <summary>
/// Minimal demo tool used for policy-denial scenarios.
/// </summary>
public sealed class ExfiltrateTool : ITool
{
    public string Name => "exfiltrate";

    public Task<string> ExecuteAsync(string input)
        => Task.FromResult($"EXFILTRATED: {input}");
}
