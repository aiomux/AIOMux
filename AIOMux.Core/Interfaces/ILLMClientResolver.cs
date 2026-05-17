namespace AIOMux.Core.Interfaces;

/// <summary>
/// Resolves named LLM clients from the configured profile set.
/// </summary>
public interface ILLMClientResolver
{
    /// <summary>
    /// Resolves a client by profile name.
    /// </summary>
    /// <param name="profileName">Profile name to resolve.</param>
    /// <returns>The resolved client.</returns>
    ILLMClient Resolve(string profileName);

    /// <summary>
    /// Attempts to resolve a client by profile name.
    /// </summary>
    /// <param name="profileName">Profile name to resolve.</param>
    /// <param name="client">Resolved client when found.</param>
    /// <returns>True when a matching profile exists; otherwise false.</returns>
    bool TryResolve(string profileName, out ILLMClient client);
}
