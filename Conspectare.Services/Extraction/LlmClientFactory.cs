using Conspectare.Services.Interfaces;
namespace Conspectare.Services.Extraction;
public class LlmClientFactory : ILlmClientFactory
{
    private readonly Dictionary<string, ILlmApiClient> _clients;
    public LlmClientFactory(Dictionary<string, ILlmApiClient> clients)
    {
        _clients = clients;
    }
    public ILlmApiClient GetClient(string providerKey)
    {
        if (!_clients.TryGetValue(providerKey, out var client))
            throw new ArgumentException($"No LLM client registered for provider '{providerKey}'.");
        return client;
    }
    public IReadOnlyList<string> GetConfiguredProviders() => _clients.Keys.ToList().AsReadOnly();
}
