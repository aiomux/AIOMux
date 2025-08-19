namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract for tools that can be used by agents to perform specific tasks.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Unique name used for tool lookup.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Execute the tool with the given input and return a result string.
    /// </summary>
    /// <param name="input">The input string for the tool</param>
    /// <returns>The result of tool execution</returns>
    Task<string> ExecuteAsync(string input);
}