using Conspectare.Domain.Entities;
using Conspectare.Services.Models;

namespace Conspectare.Services.Interfaces;

public interface IDocumentProcessor
{
    bool CanProcess(string inputFormat, string contentType);
    Task<TriageResult> TriageAsync(Document doc, Stream rawFile, CancellationToken ct);
    Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, CancellationToken ct);
    Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, ILlmApiClient llmClient, CancellationToken ct);
}
