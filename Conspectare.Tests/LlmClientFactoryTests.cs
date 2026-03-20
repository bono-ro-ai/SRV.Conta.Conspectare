using Conspectare.Services.Extraction;
using Conspectare.Services.Interfaces;
using Moq;
using Xunit;
namespace Conspectare.Tests;
public class LlmClientFactoryTests
{
    private readonly Mock<ILlmApiClient> _claudeClient = new();
    private readonly Mock<ILlmApiClient> _geminiClient = new();
    private readonly LlmClientFactory _factory;
    public LlmClientFactoryTests()
    {
        var clients = new Dictionary<string, ILlmApiClient>
        {
            ["claude"] = _claudeClient.Object,
            ["gemini"] = _geminiClient.Object
        };
        _factory = new LlmClientFactory(clients);
    }
    [Fact]
    public void GetClient_RegisteredKey_ReturnsClient()
    {
        var client = _factory.GetClient("claude");
        Assert.Same(_claudeClient.Object, client);
    }
    [Fact]
    public void GetClient_UnknownKey_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _factory.GetClient("openai"));
    }
    [Fact]
    public void GetConfiguredProviders_ReturnsAllKeys()
    {
        var providers = _factory.GetConfiguredProviders();
        Assert.Equal(2, providers.Count);
        Assert.Contains("claude", providers);
        Assert.Contains("gemini", providers);
    }
}
