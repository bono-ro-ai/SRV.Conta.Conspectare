using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
namespace Conspectare.Services.Extraction;
public class HighestConfidenceStrategy : IConsensusStrategy
{
    public ConsensusResult Resolve(IList<(string ProviderKey, ExtractionResult Result)> results)
    {
        if (results == null || results.Count == 0)
            throw new InvalidOperationException("Cannot resolve consensus from an empty result set.");
        if (results.Count == 1)
        {
            var single = results[0];
            return new ConsensusResult(single.Result, single.ProviderKey, "single_model", results);
        }
        var winner = results
            .OrderBy(r => r.Result.ReviewFlags?.Count ?? 0)
            .ThenBy(r => r.Result.LatencyMs ?? int.MaxValue)
            .ThenBy(r => r.ProviderKey)
            .First();
        return new ConsensusResult(winner.Result, winner.ProviderKey, "highest_confidence", results);
    }
}
