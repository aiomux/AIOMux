using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIOMux.Core;

internal static class DeterministicCallId
{
    public static string Generate(string runId, string stepIndex, string toolName, string jsonArgs)
    {
        var normalizedArgs = NormalizeJsonArgs(jsonArgs);
        var input = $"{runId}:{stepIndex}:{toolName}:{normalizedArgs}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash);
    }

    public static string NormalizeJsonArgs(string jsonArgs)
    {
        try
        {
            JsonDocument.Parse(jsonArgs);
            return jsonArgs.Trim();
        }
        catch (JsonException)
        {
            return jsonArgs;
        }
    }
}
