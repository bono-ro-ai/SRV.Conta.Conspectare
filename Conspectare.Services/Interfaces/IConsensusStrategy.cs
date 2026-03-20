using Conspectare.Services.Models;
namespace Conspectare.Services.Interfaces;
public interface IConsensusStrategy
{
    ConsensusResult Resolve(IList<(string ProviderKey, ExtractionResult Result)> results);
}
