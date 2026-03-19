using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Microsoft.Extensions.Logging;

namespace Conspectare.Services.Processors;

public class PdfDocumentProcessor : IDocumentProcessor
{
    private readonly IClaudeApiClient _claudeApiClient;
    private readonly ILogger<PdfDocumentProcessor> _logger;

    public PdfDocumentProcessor(IClaudeApiClient claudeApiClient, ILogger<PdfDocumentProcessor> logger)
    {
        _claudeApiClient = claudeApiClient;
        _logger = logger;
    }

    public bool CanProcess(string inputFormat, string contentType) =>
        inputFormat == InputFormat.Pdf;

    public async Task<TriageResult> TriageAsync(Document doc, Stream rawFile, CancellationToken ct)
    {
        _logger.LogInformation("Triaging PDF document {DocumentId} via Claude vision", doc.Id);

        return await _claudeApiClient.TriageAsync(
            doc,
            rawFile,
            PromptProvider.GetTriagePromptVersion(),
            ct);
    }

    public async Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, CancellationToken ct)
    {
        _logger.LogInformation("Extracting PDF document {DocumentId} via Claude vision", doc.Id);

        return await _claudeApiClient.ExtractAsync(
            doc,
            rawFile,
            doc.DocumentType,
            PromptProvider.GetExtractionPromptVersion(doc.DocumentType),
            ct);
    }
}
