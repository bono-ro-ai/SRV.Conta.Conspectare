using Conspectare.Domain.Entities;
using Conspectare.Services.Models;
namespace Conspectare.Services.Interfaces;
public interface ILlmApiClient
{
    Task<TriageResult> TriageAsync(Document doc, Stream rawFile, string promptText, string promptVersion, CancellationToken ct = default);
    Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, string documentType, string promptText, string promptVersion, CancellationToken ct = default);
}
