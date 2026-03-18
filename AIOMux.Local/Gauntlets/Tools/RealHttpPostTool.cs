using AIOMux.Core.Interfaces;
using System.Text;
using System.Text.Json;

namespace AIOMux.Local.Gauntlets.Tools;

/// <summary>
/// Live http.post tool that makes real outbound HTTP POST requests.
/// Used in the happy-path fork of the gauntlet when --live is specified.
/// WARNING: this will contact the URL in the tool arguments.
/// </summary>
internal sealed class RealHttpPostTool : ITool
{
    private static readonly HttpClient _http = new();

    public string Name => "http.post";

    public async Task<string> ExecuteAsync(string input)
    {
        using var doc = JsonDocument.Parse(input);
        var root = doc.RootElement;

        if (!root.TryGetProperty("url", out var urlProp))
        {
            throw new ArgumentException("http.post requires a 'url' argument in the JSON args.");
        }

        var url = urlProp.GetString()
                  ?? throw new ArgumentException("'url' must be a non-null string.");

        var body = root.TryGetProperty("body", out var bodyProp)
            ? bodyProp.GetString() ?? string.Empty
            : string.Empty;

        var contentType = root.TryGetProperty("contentType", out var ctProp)
            ? ctProp.GetString() ?? "application/json"
            : "application/json";

        using var content = new StringContent(body, Encoding.UTF8, contentType);
        using var response = await _http.PostAsync(url, content);

        return JsonSerializer.Serialize(new
        {
            status = (int)response.StatusCode,
            body = await response.Content.ReadAsStringAsync()
        });
    }
}
