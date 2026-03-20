using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.Processors;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Conspectare.Tests;

public class PromptServiceTests
{
    private readonly PromptService _service;

    public PromptServiceTests()
    {
        _service = new PromptService(NullLogger<PromptService>.Instance);
    }

    [Fact]
    public void GetPrompt_NoDbVersions_FallsBackToTriageEmbedded()
    {
        var (promptText, version) = _service.GetPrompt("triage", null);

        Assert.Equal(PromptProvider.GetTriagePrompt(), promptText);
        Assert.Equal(PromptProvider.GetTriagePromptVersion(), version);
    }

    [Fact]
    public void GetPrompt_NoDbVersions_FallsBackToExtractionInvoiceEmbedded()
    {
        var (promptText, version) = _service.GetPrompt("extraction", "invoice");

        Assert.Equal(PromptProvider.GetExtractionPrompt("invoice"), promptText);
        Assert.Equal(PromptProvider.GetExtractionPromptVersion("invoice"), version);
    }

    [Fact]
    public void GetPrompt_NoDbVersions_FallsBackToExtractionReceiptEmbedded()
    {
        var (promptText, version) = _service.GetPrompt("extraction", "receipt");

        Assert.Equal(PromptProvider.GetExtractionPrompt("receipt"), promptText);
        Assert.Equal(PromptProvider.GetExtractionPromptVersion("receipt"), version);
    }

    [Fact]
    public void GetPrompt_UnknownPhase_FallsBackToTriageEmbedded()
    {
        var (promptText, version) = _service.GetPrompt("unknown_phase", null);

        Assert.Equal(PromptProvider.GetTriagePrompt(), promptText);
        Assert.Equal(PromptProvider.GetTriagePromptVersion(), version);
    }

    [Fact]
    public void GetPrompt_CalledTwice_ReturnsSameResultFromCache()
    {
        var result1 = _service.GetPrompt("triage", null);
        var result2 = _service.GetPrompt("triage", null);

        Assert.Equal(result1.PromptText, result2.PromptText);
        Assert.Equal(result1.Version, result2.Version);
    }

    [Fact]
    public void GetPrompt_DifferentPhases_ReturnDifferentResults()
    {
        var triageResult = _service.GetPrompt("triage", null);
        var extractionResult = _service.GetPrompt("extraction", "invoice");

        Assert.NotEqual(triageResult.Version, extractionResult.Version);
    }
}
