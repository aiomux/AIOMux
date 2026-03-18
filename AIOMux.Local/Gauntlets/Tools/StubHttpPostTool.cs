using AIOMux.Core.Interfaces;
using System.Text.Json;

namespace AIOMux.Local.Gauntlets.Tools;

/// <summary>
/// Stub http.post tool used in the happy-path fork to simulate a successful HTTP POST
/// without actually making any network calls.
/// </summary>
internal sealed class StubHttpPostTool : ITool
{
    public string Name => "http.post";

    public Task<string> ExecuteAsync(string input)
    {
        var response = JsonSerializer.Serialize(new
        {
            status = 200,
            body = "OK",
            stub = true
        });

        return Task.FromResult(response);
    }
}
