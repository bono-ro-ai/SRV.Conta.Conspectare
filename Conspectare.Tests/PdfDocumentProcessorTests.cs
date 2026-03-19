using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Processors;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Conspectare.Tests;

public class PdfDocumentProcessorTests
{
    private readonly Mock<IClaudeApiClient> _claudeApiClientMock = new();
    private readonly Mock<ILogger<PdfDocumentProcessor>> _loggerMock = new();
    private readonly PdfDocumentProcessor _processor;

    public PdfDocumentProcessorTests()
    {
        _processor = new PdfDocumentProcessor(_claudeApiClientMock.Object, _loggerMock.Object);
    }

    private static Document CreateTestDocument(string inputFormat = null, string documentType = "invoice") => new()
    {
        Id = 1,
        TenantId = 100,
        InputFormat = inputFormat ?? InputFormat.Pdf,
        ContentType = "application/pdf",
        FileName = "invoice.pdf",
        DocumentType = documentType
    };

    [Fact]
    public void CanProcess_Pdf_ReturnsTrue()
    {
        Assert.True(_processor.CanProcess(InputFormat.Pdf, "application/pdf"));
    }

    [Fact]
    public void CanProcess_Image_ReturnsFalse()
    {
        Assert.False(_processor.CanProcess(InputFormat.Image, "image/jpeg"));
    }

    [Fact]
    public async Task TriageAsync_DelegatesToClaudeApi()
    {
        var doc = CreateTestDocument();
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF

        var expectedResult = new TriageResult(
            DocumentType: "invoice",
            Confidence: 0.95m,
            IsAccountingRelevant: true,
            ModelId: "claude-sonnet-4-20250514",
            PromptVersion: PromptProvider.GetTriagePromptVersion(),
            InputTokens: 1200,
            OutputTokens: 50,
            LatencyMs: 3200);

        _claudeApiClientMock
            .Setup(c => c.TriageAsync(
                doc,
                stream,
                PromptProvider.GetTriagePromptVersion(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await _processor.TriageAsync(doc, stream, CancellationToken.None);

        Assert.Equal(expectedResult, result);
        _claudeApiClientMock.Verify(c => c.TriageAsync(
            doc,
            stream,
            PromptProvider.GetTriagePromptVersion(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExtractAsync_DelegatesToClaudeApi()
    {
        var doc = CreateTestDocument(documentType: "invoice");
        using var stream = new MemoryStream(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // %PDF

        var expectedResult = new ExtractionResult(
            OutputJson: "{\"invoice_number\":\"FA-001\"}",
            SchemaVersion: "1.0.0",
            ModelId: "claude-sonnet-4-20250514",
            PromptVersion: PromptProvider.GetExtractionPromptVersion("invoice"),
            InputTokens: 1500,
            OutputTokens: 200,
            LatencyMs: 4500,
            ReviewFlags: new List<ReviewFlagInfo>());

        _claudeApiClientMock
            .Setup(c => c.ExtractAsync(
                doc,
                stream,
                "invoice",
                PromptProvider.GetExtractionPromptVersion("invoice"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await _processor.ExtractAsync(doc, stream, CancellationToken.None);

        Assert.Equal(expectedResult, result);
        _claudeApiClientMock.Verify(c => c.ExtractAsync(
            doc,
            stream,
            "invoice",
            PromptProvider.GetExtractionPromptVersion("invoice"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
