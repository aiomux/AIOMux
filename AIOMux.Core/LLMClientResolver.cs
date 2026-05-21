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
    /// <param name="clients">Named profile map.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="clients"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when duplicate profile names or invalid entries are detected.</exception>
    public LLMClientResolver(IReadOnlyDictionary<string, ILLMClient> clients)
    {
        ArgumentNullException.ThrowIfNull(clients);

        _profiles = new Dictionary<string, ILLMClient>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, client) in clients)
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
    public ILLMClient GetRequired(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            throw new ArgumentException("LLM profile name must be a non-empty, non-whitespace string.", nameof(profileName));

        if (_profiles.TryGetValue(profileName, out var client))
            return client;

        throw new InvalidOperationException($"LLM profile '{profileName}' was not found.");
    }

    /// <inheritdoc />
    public ILLMClient? TryGet(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
            return null;

        return _profiles.TryGetValue(profileName, out var client) ? client : null;
    }
}
