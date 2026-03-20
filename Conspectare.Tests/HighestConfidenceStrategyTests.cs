using Conspectare.Services.Extraction;
using Conspectare.Services.Models;
using Xunit;
namespace Conspectare.Tests;
public class HighestConfidenceStrategyTests
{
    private readonly HighestConfidenceStrategy _strategy = new();
    private static ExtractionResult CreateResult(int flagCount = 0, int latencyMs = 1000, string modelId = "model-1")
    {
        var flags = new List<ReviewFlagInfo>();
        for (var i = 0; i < flagCount; i++)
            flags.Add(new ReviewFlagInfo("test_flag", "warning", $"Flag {i + 1}"));
        return new ExtractionResult(
            OutputJson: "{\"test\":true}",
            SchemaVersion: "1.0.0",
            ModelId: modelId,
            PromptVersion: "v1",
            InputTokens: 100,
            OutputTokens: 50,
            LatencyMs: latencyMs,
            ReviewFlags: flags);
    }
    [Fact]
    public void Resolve_SingleResult_ReturnsSingleModelStrategy()
    {
        var results = new List<(string ProviderKey, ExtractionResult Result)>
        {
            ("claude", CreateResult())
        };
        var consensus = _strategy.Resolve(results);
        Assert.Equal("single_model", consensus.StrategyUsed);
        Assert.Equal("claude", consensus.WinningProviderKey);
        Assert.Same(results[0].Result, consensus.WinningResult);
    }
    [Fact]
    public void Resolve_TwoResults_FewerFlagsWins()
    {
        var results = new List<(string ProviderKey, ExtractionResult Result)>
        {
            ("claude", CreateResult(flagCount: 2, latencyMs: 500)),
            ("gemini", CreateResult(flagCount: 0, latencyMs: 2000))
        };
        var consensus = _strategy.Resolve(results);
        Assert.Equal("highest_confidence", consensus.StrategyUsed);
        Assert.Equal("gemini", consensus.WinningProviderKey);
    }
    [Fact]
    public void Resolve_SameFlagCount_LowerLatencyWins()
    {
        var results = new List<(string ProviderKey, ExtractionResult Result)>
        {
            ("claude", CreateResult(flagCount: 1, latencyMs: 3000)),
            ("gemini", CreateResult(flagCount: 1, latencyMs: 1500))
        };
        var consensus = _strategy.Resolve(results);
        Assert.Equal("highest_confidence", consensus.StrategyUsed);
        Assert.Equal("gemini", consensus.WinningProviderKey);
    }
    [Fact]
    public void Resolve_EmptyResults_Throws()
    {
        var results = new List<(string ProviderKey, ExtractionResult Result)>();
        Assert.Throws<InvalidOperationException>(() => _strategy.Resolve(results));
    }
    [Fact]
    public void Resolve_NullResults_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _strategy.Resolve(null));
    }
    [Fact]
    public void Resolve_AllResultsReturned()
    {
        var results = new List<(string ProviderKey, ExtractionResult Result)>
        {
            ("claude", CreateResult(flagCount: 0)),
            ("gemini", CreateResult(flagCount: 1))
        };
        var consensus = _strategy.Resolve(results);
        Assert.Equal(2, consensus.AllResults.Count);
    }
}
