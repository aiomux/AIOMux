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

    public static async Task SaveAsync(
        string runId,
        IReadOnlyList<ExecutionRecord> records,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var filePath = GetRunFilePath(runId, workingDirectory);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        await JsonSerializer.SerializeAsync(fs, records, JsonOptions, cancellationToken);
    }

    public static async Task<List<ExecutionRecord>> LoadAsync(
        string runId,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var filePath in GetCandidateRunFilePaths(runId, workingDirectory))
        {
            if (!File.Exists(filePath))
                continue;

            await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var records = await JsonSerializer.DeserializeAsync<List<ExecutionRecord>>(fs, JsonOptions, cancellationToken);
            return records ?? [];
        }

        return [];
    }

    public static string GetRunFilePath(string runId, string? workingDirectory = null)
    {
        var runsDirectory = ResolveRunsDirectory(workingDirectory);
        return Path.Combine(runsDirectory, $"{runId}.records.json");
    }

    private static IEnumerable<string> GetCandidateRunFilePaths(string runId, string? workingDirectory)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidatePaths = new List<string>();

        void AddPath(string path)
        {
            if (seen.Add(path))
                candidatePaths.Add(path);
        }

        AddPath(Path.Combine(ResolveRunsDirectory(workingDirectory), $"{runId}.records.json"));
        AddPath(Path.Combine(ResolveRunsDirectory(Environment.CurrentDirectory), $"{runId}.records.json"));

        return candidatePaths;
    }

    private static string ResolveRunsDirectory(string? workingDirectory)
    {
        var rootDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Environment.CurrentDirectory
            : Path.GetFullPath(workingDirectory);

        return Path.Combine(rootDirectory, "runs");
    }
}
