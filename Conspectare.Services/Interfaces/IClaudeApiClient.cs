using Conspectare.Domain.Entities;
using Conspectare.Services.Models;

namespace Conspectare.Services.Interfaces;

public interface IClaudeApiClient
{
    Task<TriageResult> TriageAsync(Document doc, Stream rawFile, string promptVersion, CancellationToken ct = default);
    Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, string documentType, string promptVersion, CancellationToken ct = default);
}
