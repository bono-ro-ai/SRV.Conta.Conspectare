using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;

namespace Conspectare.Services.Extraction;

/// <summary>
/// Consensus strategy that selects the extraction result with the fewest review flags,
/// breaking ties by lowest latency and then alphabetical provider key.
/// </summary>
public class HighestConfidenceStrategy : IConsensusStrategy
{
    /// <summary>
    /// Evaluates all provider results and returns the one deemed most reliable.
    /// With a single result, it is returned unconditionally.
    /// With multiple results, the winner is chosen by: fewest review flags → lowest latency → provider key (asc).
    /// </summary>
    public ConsensusResult Resolve(IList<(string ProviderKey, ExtractionResult Result)> results)
    {
        if (results == null || results.Count == 0)
            throw new InvalidOperationException("Cannot resolve consensus from an empty result set.");

        // Short-circuit when only one provider responded successfully.
        if (results.Count == 1)
        {
            var single = results[0];
            return new ConsensusResult(single.Result, single.ProviderKey, "single_model", results);
        }

        // Prefer results with fewer review flags (higher confidence), then break ties deterministically.
        var winner = results
            .OrderBy(r => r.Result.ReviewFlags?.Count ?? 0)
            .ThenBy(r => r.Result.LatencyMs ?? int.MaxValue)
            .ThenBy(r => r.ProviderKey)
            .First();

        return new ConsensusResult(winner.Result, winner.ProviderKey, "highest_confidence", results);
    }
}
