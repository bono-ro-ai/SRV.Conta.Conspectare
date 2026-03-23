using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
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
        var (promptText, version) = _service.GetPrompt(PipelinePhase.Triage, null);

        Assert.Equal(PromptProvider.GetTriagePrompt(), promptText);
        Assert.Equal(PromptProvider.GetTriagePromptVersion(), version);
    }

    [Fact]
    public void GetPrompt_NoDbVersions_FallsBackToExtractionInvoiceEmbedded()
    {
        var (promptText, version) = _service.GetPrompt(PipelinePhase.Extraction, "invoice");

        Assert.Equal(PromptProvider.GetExtractionPrompt("invoice"), promptText);
        Assert.Equal(PromptProvider.GetExtractionPromptVersion("invoice"), version);
    }

    [Fact]
    public void GetPrompt_NoDbVersions_FallsBackToExtractionReceiptEmbedded()
    {
        var (promptText, version) = _service.GetPrompt(PipelinePhase.Extraction, "receipt");

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
        var result1 = _service.GetPrompt(PipelinePhase.Triage, null);
        var result2 = _service.GetPrompt(PipelinePhase.Triage, null);

        Assert.Equal(result1.PromptText, result2.PromptText);
        Assert.Equal(result1.Version, result2.Version);
    }

    [Fact]
    public void GetPrompt_DifferentPhases_ReturnDifferentResults()
    {
        var triageResult = _service.GetPrompt(PipelinePhase.Triage, null);
        var extractionResult = _service.GetPrompt(PipelinePhase.Extraction, "invoice");

        Assert.NotEqual(triageResult.Version, extractionResult.Version);
    }

    [Fact]
    public void SelectByWeight_SingleVersion_ReturnsIt()
    {
        var version = new PromptVersion { Version = "v1", PromptText = "test", TrafficWeight = 100 };
        var result = _service.SelectByWeight(new List<PromptVersion> { version });

        Assert.Equal("v1", result.Version);
    }

    [Fact]
    public void SelectByWeight_AllZeroWeights_DoesNotThrow()
    {
        var versions = new List<PromptVersion>
        {
            new() { Version = "v1", PromptText = "a", TrafficWeight = 0 },
            new() { Version = "v2", PromptText = "b", TrafficWeight = 0 }
        };

        var result = _service.SelectByWeight(versions);
        Assert.NotNull(result);
    }

    [Fact]
    public void SelectByWeight_TwoVersions_ReturnsOneOfThem()
    {
        var versions = new List<PromptVersion>
        {
            new() { Version = "v1", PromptText = "a", TrafficWeight = 70 },
            new() { Version = "v2", PromptText = "b", TrafficWeight = 30 }
        };

        var selectedVersions = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            var result = _service.SelectByWeight(versions);
            selectedVersions.Add(result.Version);
        }

        Assert.Contains("v1", selectedVersions);
        Assert.Contains("v2", selectedVersions);
    }
}
