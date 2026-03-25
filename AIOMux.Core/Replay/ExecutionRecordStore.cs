using AIOMux.Core.Models;
using System.Text.Json;

namespace AIOMux.Core.Replay;

internal static class ExecutionRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static async Task SaveAsync(string runId, IReadOnlyList<ExecutionRecord> records, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = GetRunFilePath(runId);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(fs, records, JsonOptions, cancellationToken);
    }

    public static async Task<List<ExecutionRecord>> LoadAsync(string runId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = GetRunFilePath(runId);
        if (!File.Exists(filePath))
            return [];

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var records = await JsonSerializer.DeserializeAsync<List<ExecutionRecord>>(fs, JsonOptions, cancellationToken);
        return records ?? [];
    }

    public static string GetRunFilePath(string runId)
    {
        var runsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".aiomux",
            "runs");

        return Path.Combine(runsDirectory, $"{runId}.records.json");
    }
}
