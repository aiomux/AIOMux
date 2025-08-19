using AIOMux.Core.Interfaces;

namespace AIOMux.Core.Memory;

/// <summary>
/// Simple in-memory implementation of IMemoryStore using a dictionary.
/// </summary>
public class InMemoryStore : IMemoryStore
{
    private readonly Dictionary<string, string> _storage = new();

    /// <summary>
    /// Stores a value with the specified key.
    /// </summary>
    /// <param name="key">The key to store the value under</param>
    /// <param name="value">The value to store</param>
    public Task StoreAsync(string key, string value)
    {
        _storage[key] = value;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves a value by key.
    /// </summary>
    /// <param name="key">The key to retrieve</param>
    /// <returns>The stored value, or null if not found</returns>
    public Task<string?> RetrieveAsync(string key)
    {
        if (_storage.TryGetValue(key, out var value))
        {
            return Task.FromResult<string?>(value);
        }
        return Task.FromResult<string?>(null);
    }
}