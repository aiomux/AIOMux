using AIOMux.Core.Interfaces;

namespace AIOMux.Core.Memory;

/// <summary>
/// No-op implementation of IMemoryStore that discards all data.
/// </summary>
public class NullMemoryStore : IMemoryStore
{
    /// <summary>
    /// Stores a value (no-op).
    /// </summary>
    public Task StoreAsync(string key, string value)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves a value (always returns null).
    /// </summary>
    public Task<string?> RetrieveAsync(string key)
    {
        return Task.FromResult<string?>(null);
    }
}
