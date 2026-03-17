using AIOMux.Core.Interfaces;
using System.Text.Json;

namespace AIOMux.Core.Memory;

/// <summary>
/// JSON-file-backed memory store that persists key/value data across process runs.
/// </summary>
public sealed class JsonFileMemoryStore : IMemoryStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private Dictionary<string, string>? _cache;

    public JsonFileMemoryStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aiomux",
            "memory.json");
    }

    public async Task StoreAsync(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Memory key cannot be null or whitespace.", nameof(key));
        }

        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            _cache![key] = value;
            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> RetrieveAsync(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            return _cache!.TryGetValue(key, out var value) ? value : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    private Task EnsureLoadedAsync()
    {
        if (_cache != null)
        {
            return Task.CompletedTask;
        }

        if (!File.Exists(_filePath))
        {
            _cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return Task.CompletedTask;
        }

        var json = File.ReadAllText(_filePath);
        _cache = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return Task.CompletedTask;
    }

    private Task PersistAsync()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);

        return Task.CompletedTask;
    }
}
