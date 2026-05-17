using AIOMux.Core.Interfaces;

namespace AIOMux.Core;

/// <summary>
/// Default named LLM client resolver.
/// </summary>
public sealed class LLMClientResolver : ILLMClientResolver
{
    private readonly Dictionary<string, ILLMClient> _profiles;

    /// <summary>
    /// Creates a resolver from a profile map.
    /// </summary>
    /// <param name="profiles">Named profile map.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="profiles"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when duplicate profile names are detected.</exception>
    public LLMClientResolver(IEnumerable<KeyValuePair<string, ILLMClient>> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        _profiles = new Dictionary<string, ILLMClient>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, client) in profiles)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidOperationException("Invalid profile config: profile name cannot be empty.");

            if (client == null)
                throw new InvalidOperationException($"Invalid profile config: profile '{name}' has no client instance.");

            if (!_profiles.TryAdd(name, client))
                throw new InvalidOperationException($"Duplicate LLM profile '{name}' is not allowed.");
        }
    }

    /// <inheritdoc />
    public ILLMClient Resolve(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            throw new InvalidOperationException("Unknown LLM profile ''.");

        if (TryResolve(profileName, out var client))
            return client;

        throw new InvalidOperationException($"Unknown LLM profile '{profileName}'.");
    }

    /// <inheritdoc />
    public bool TryResolve(string profileName, out ILLMClient client)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            client = default!;
            return false;
        }

        return _profiles.TryGetValue(profileName, out client!);
    }
}
