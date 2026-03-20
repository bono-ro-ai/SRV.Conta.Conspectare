namespace Conspectare.Services.Models;
public record ConsensusResult(
    ExtractionResult WinningResult,
    string WinningProviderKey,
    string StrategyUsed,
    IList<(string ProviderKey, ExtractionResult Result)> AllResults);
