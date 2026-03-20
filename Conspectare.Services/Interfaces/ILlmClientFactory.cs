namespace Conspectare.Services.Interfaces;
public interface ILlmClientFactory
{
    ILlmApiClient GetClient(string providerKey);
    IReadOnlyList<string> GetConfiguredProviders();
}
