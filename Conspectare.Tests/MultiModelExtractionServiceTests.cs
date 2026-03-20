using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Extraction;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
namespace Conspectare.Tests;
public class MultiModelExtractionServiceTests
{
    private readonly Mock<ILlmClientFactory> _factoryMock = new();
    private readonly Mock<IProcessorRegistry> _registryMock = new();
    private readonly Mock<IConsensusStrategy> _strategyMock = new();
    private readonly Mock<IPromptService> _promptServiceMock = new();
    private readonly Mock<ILogger<MultiModelExtractionService>> _loggerMock = new();
    private readonly MultiModelSettings _settings;
    private readonly MultiModelExtractionService _service;
    public MultiModelExtractionServiceTests()
    {
        _settings = new MultiModelSettings
        {
            Enabled = true,
            Providers = new List<string> { "claude", "gemini" },
            ConsensusStrategy = "highest_confidence",
            TimeoutSeconds = 120
        };
        _service = new MultiModelExtractionService(
            _factoryMock.Object,
            _registryMock.Object,
            _strategyMock.Object,
            _promptServiceMock.Object,
            Options.Create(_settings),
            _loggerMock.Object);
    }
    private static Document CreateTestDocument() => new()
    {
        Id = 1,
        TenantId = 100,
        InputFormat = InputFormat.Image,
        ContentType = "image/jpeg",
        FileName = "test.jpg",
        DocumentType = "invoice"
    };
    private static ExtractionResult CreateExtractionResult(string modelId = "model-1", int latencyMs = 1000) =>
        new(
            OutputJson: "{\"test\":true}",
            SchemaVersion: "1.0.0",
            ModelId: modelId,
            PromptVersion: "v1",
            InputTokens: 100,
            OutputTokens: 50,
            LatencyMs: latencyMs,
            ReviewFlags: new List<ReviewFlagInfo>());
    [Fact]
    public async Task ExtractAsync_TwoProvidersSucceed_ReturnsConsensusResult()
    {
        var doc = CreateTestDocument();
        var claudeResult = CreateExtractionResult("claude-model", 1000);
        var geminiResult = CreateExtractionResult("gemini-model", 1500);
        var claudeClient = new Mock<ILlmApiClient>();
        var geminiClient = new Mock<ILlmApiClient>();
        var processor = new Mock<IDocumentProcessor>();
        _factoryMock.Setup(f => f.GetClient("claude")).Returns(claudeClient.Object);
        _factoryMock.Setup(f => f.GetClient("gemini")).Returns(geminiClient.Object);
        _registryMock.Setup(r => r.Resolve(doc.InputFormat, doc.ContentType)).Returns(processor.Object);
        processor.Setup(p => p.ExtractAsync(doc, It.IsAny<Stream>(), claudeClient.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claudeResult);
        processor.Setup(p => p.ExtractAsync(doc, It.IsAny<Stream>(), geminiClient.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geminiResult);
        var expectedConsensus = new ConsensusResult(
            claudeResult, "claude", "highest_confidence",
            new List<(string, ExtractionResult)> { ("claude", claudeResult), ("gemini", geminiResult) });
        _strategyMock.Setup(s => s.Resolve(It.IsAny<IList<(string, ExtractionResult)>>()))
            .Returns(expectedConsensus);
        var result = await _service.ExtractAsync(doc, new byte[] { 0xFF, 0xD8 }, CancellationToken.None);
        Assert.Equal(expectedConsensus, result);
        _strategyMock.Verify(s => s.Resolve(It.Is<IList<(string, ExtractionResult)>>(
            r => r.Count == 2)), Times.Once);
    }
    [Fact]
    public async Task ExtractAsync_OneProviderFails_ReturnsResultFromSuccessfulProvider()
    {
        var doc = CreateTestDocument();
        var geminiResult = CreateExtractionResult("gemini-model", 1500);
        var claudeClient = new Mock<ILlmApiClient>();
        var geminiClient = new Mock<ILlmApiClient>();
        var processor = new Mock<IDocumentProcessor>();
        _factoryMock.Setup(f => f.GetClient("claude")).Returns(claudeClient.Object);
        _factoryMock.Setup(f => f.GetClient("gemini")).Returns(geminiClient.Object);
        _registryMock.Setup(r => r.Resolve(doc.InputFormat, doc.ContentType)).Returns(processor.Object);
        processor.Setup(p => p.ExtractAsync(doc, It.IsAny<Stream>(), claudeClient.Object, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Claude API error"));
        processor.Setup(p => p.ExtractAsync(doc, It.IsAny<Stream>(), geminiClient.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(geminiResult);
        var expectedConsensus = new ConsensusResult(
            geminiResult, "gemini", "single_model",
            new List<(string, ExtractionResult)> { ("gemini", geminiResult) });
        _strategyMock.Setup(s => s.Resolve(It.IsAny<IList<(string, ExtractionResult)>>()))
            .Returns(expectedConsensus);
        var result = await _service.ExtractAsync(doc, new byte[] { 0xFF, 0xD8 }, CancellationToken.None);
        Assert.Equal(expectedConsensus, result);
        _strategyMock.Verify(s => s.Resolve(It.Is<IList<(string, ExtractionResult)>>(
            r => r.Count == 1)), Times.Once);
    }
    [Fact]
    public async Task ExtractAsync_AllProvidersFail_ThrowsAggregateException()
    {
        var doc = CreateTestDocument();
        var claudeClient = new Mock<ILlmApiClient>();
        var geminiClient = new Mock<ILlmApiClient>();
        var processor = new Mock<IDocumentProcessor>();
        _factoryMock.Setup(f => f.GetClient("claude")).Returns(claudeClient.Object);
        _factoryMock.Setup(f => f.GetClient("gemini")).Returns(geminiClient.Object);
        _registryMock.Setup(r => r.Resolve(doc.InputFormat, doc.ContentType)).Returns(processor.Object);
        processor.Setup(p => p.ExtractAsync(doc, It.IsAny<Stream>(), claudeClient.Object, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Claude API error"));
        processor.Setup(p => p.ExtractAsync(doc, It.IsAny<Stream>(), geminiClient.Object, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Gemini API error"));
        await Assert.ThrowsAsync<AggregateException>(
            () => _service.ExtractAsync(doc, new byte[] { 0xFF, 0xD8 }, CancellationToken.None));
    }
}
