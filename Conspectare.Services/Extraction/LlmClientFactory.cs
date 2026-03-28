using Conspectare.Services.Interfaces;

namespace Conspectare.Services.Extraction;

/// <summary>
/// Resolves registered <see cref="ILlmApiClient"/> instances by provider key
/// (e.g. "claude", "gemini"). Clients are injected at startup via DI.
/// </summary>
public class LlmClientFactory : ILlmClientFactory
{
    private readonly Dictionary<string, ILlmApiClient> _clients;

    public LlmClientFactory(Dictionary<string, ILlmApiClient> clients)
    {
        _clients = clients;
    }

    /// <summary>
    /// Returns the <see cref="ILlmApiClient"/> registered under <paramref name="providerKey"/>.
    /// Throws <see cref="ArgumentException"/> if no client is registered for that key.
    /// </summary>
    public ILlmApiClient GetClient(string providerKey)
    {
        if (!_clients.TryGetValue(providerKey, out var client))
            throw new ArgumentException($"No LLM client registered for provider '{providerKey}'.");

        return client;
    }

    /// <summary>
    /// Returns the keys of all providers currently registered in the factory.
    /// </summary>
    public IReadOnlyList<string> GetConfiguredProviders() => _clients.Keys.ToList().AsReadOnly();
}
