namespace AIOMux.Core.Interfaces;

/// <summary>
/// Resolves named LLM clients from the configured profile set.
/// </summary>
public interface ILLMClientResolver
{
    /// <summary>
    /// Resolves a client by profile name, throwing if the name is blank or the profile is not found.
    /// </summary>
    /// <param name="profileName">Profile name to resolve.</param>
    /// <returns>The resolved client.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="profileName"/> is null or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the profile name is not found.</exception>
    ILLMClient GetRequired(string profileName);

    /// <summary>
    /// Attempts to resolve a client by profile name.
    /// </summary>
    /// <param name="profileName">Profile name to resolve.</param>
    /// <returns>The resolved client when found; otherwise null.</returns>
    ILLMClient? TryGet(string profileName);
}
