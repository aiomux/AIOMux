using AIOMux.Skills.Notes.Models;
using System.Text.Json;

namespace AIOMux.Skills.Notes.Storage;

/// <summary>
/// JSON-based implementation of INotesStore.
/// Stores all notes in a single JSON file with atomic writes and in-memory caching.
/// </summary>
public class JsonNotesStore : INotesStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<NoteRecord> _cache = new();
    private DateTime _lastLoad = DateTime.MinValue;

    public string Name => "NotesStore";

    public JsonNotesStore(string? basePath = null)
    {
        var appDataPath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aiomux"
        );

        Directory.CreateDirectory(appDataPath);
        _filePath = Path.Combine(appDataPath, "notes.json");
    }

    /// <summary>
    /// ITool.ExecuteAsync implementation - parses JSON input and routes to appropriate method.
    /// </summary>
    public async Task<string> ExecuteAsync(string input)
    {
        try
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(input);
            if (json == null)
            {
                return JsonSerializer.Serialize(new { error = "Invalid JSON input" });
            }

            var action = json.TryGetValue("action", out var actionElement)
                ? actionElement.GetString()
                : null;

            switch (action?.ToLowerInvariant())
            {
                case "save":
                    var note = json.TryGetValue("note", out var noteElement)
                        ? JsonSerializer.Deserialize<NoteRecord>(noteElement.GetRawText())
                        : null;
                    if (note == null) return JsonSerializer.Serialize(new { error = "Missing note data" });
                    var id = await SaveAsync(note);
                    return JsonSerializer.Serialize(new { id });

                case "get":
                    var getId = json.TryGetValue("id", out var idElement)
                        ? idElement.GetString()
                        : null;
                    if (getId == null) return JsonSerializer.Serialize(new { error = "Missing id" });
                    var result = await GetAsync(getId);
                    return JsonSerializer.Serialize(result);

                case "search":
                    var query = json.TryGetValue("query", out var queryElement)
                        ? JsonSerializer.Deserialize<NoteQuery>(queryElement.GetRawText())
                        : new NoteQuery();
                    var results = await SearchAsync(query ?? new NoteQuery());
                    return JsonSerializer.Serialize(results);

                case "getall":
                    var all = await GetAllAsync();
                    return JsonSerializer.Serialize(all);

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown action: {action}" });
            }
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Save a note record to storage.
    /// </summary>
    public async Task<string> SaveAsync(NoteRecord note)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();

            // Check if note already exists and update, or add new
            var existing = _cache.FirstOrDefault(n => n.Id == note.Id);
            if (existing != null)
            {
                _cache.Remove(existing);
            }

            _cache.Add(note);

            await SaveToFileAsync();

            return note.Id;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get a note by ID.
    /// </summary>
    public async Task<NoteRecord?> GetAsync(string id)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            return _cache.FirstOrDefault(n => n.Id == id);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Search notes based on query parameters.
    /// </summary>
    public async Task<List<NoteRecord>> SearchAsync(NoteQuery query)
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();

            var results = _cache.AsEnumerable();

            // Filter by type
            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                results = results.Where(n => n.Type.Equals(query.Type, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by tags
            if (query.Tags != null && query.Tags.Count > 0)
            {
                results = results.Where(n => query.Tags.Any(tag =>
                    n.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
            }

            // Text search
            if (!string.IsNullOrWhiteSpace(query.Q))
            {
                var searchTerm = query.Q.ToLowerInvariant();
                results = results.Where(n =>
                    n.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    n.Summary.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                    n.OriginalText.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
            }

            return results
                .OrderByDescending(n => n.CreatedUtc)
                .Take(query.MaxResults)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get all notes.
    /// </summary>
    public async Task<List<NoteRecord>> GetAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await EnsureLoadedAsync();
            return _cache.OrderByDescending(n => n.CreatedUtc).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Ensure cache is loaded from file.
    /// </summary>
    private async Task EnsureLoadedAsync()
    {
        // If cache is empty or file has been modified, reload
        if (_cache.Count == 0 || (File.Exists(_filePath) && File.GetLastWriteTimeUtc(_filePath) > _lastLoad))
        {
            await LoadFromFileAsync();
        }
    }

    /// <summary>
    /// Load notes from JSON file into cache.
    /// </summary>
    private async Task LoadFromFileAsync()
    {
        if (!File.Exists(_filePath))
        {
            _cache = new List<NoteRecord>();
            _lastLoad = DateTime.UtcNow;
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            _cache = JsonSerializer.Deserialize<List<NoteRecord>>(json) ?? new List<NoteRecord>();
            _lastLoad = DateTime.UtcNow;
        }
        catch (Exception)
        {
            // If file is corrupted, start with empty cache
            _cache = new List<NoteRecord>();
            _lastLoad = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Save cache to JSON file with atomic write.
    /// </summary>
    private async Task SaveToFileAsync()
    {
        var tempPath = _filePath + ".tmp";

        try
        {
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(tempPath, json);

            // Atomic replace
            File.Move(tempPath, _filePath, overwrite: true);

            _lastLoad = DateTime.UtcNow;
        }
        finally
        {
            // Clean up temp file if it exists
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
