namespace AIOMux.Core.Interfaces;

/// <summary>
/// Contract for storage systems that can persist and retrieve information for agents.
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// Stores a value with the specified key.
    /// </summary>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The value to store</param>
    Task StoreAsync(string key, string value);

    /// <summary>
    /// Retrieves a value by key.
    /// </summary>
    /// <param name="key">The key to retrieve</param>
    /// <returns>The stored value, or null if not found</returns>
    Task<string?> RetrieveAsync(string key);
}