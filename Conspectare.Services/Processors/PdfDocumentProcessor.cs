using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Microsoft.Extensions.Logging;

namespace Conspectare.Services.Processors;

public class PdfDocumentProcessor : IDocumentProcessor
{
    private readonly ILlmApiClient _llmApiClient;
    private readonly IPromptService _promptService;
    private readonly ILogger<PdfDocumentProcessor> _logger;
    public PdfDocumentProcessor(ILlmApiClient llmApiClient, IPromptService promptService, ILogger<PdfDocumentProcessor> logger)
    {
        _llmApiClient = llmApiClient;
        _promptService = promptService;
        _logger = logger;
    }

    public bool CanProcess(string inputFormat, string contentType) =>
        inputFormat == InputFormat.Pdf;

    public async Task<TriageResult> TriageAsync(Document doc, Stream rawFile, CancellationToken ct)
    {
        _logger.LogInformation("Triaging PDF document {DocumentId} via Claude vision", doc.Id);

        var (promptText, promptVersion) = _promptService.GetPrompt(PipelinePhase.Triage, null);
        return await _llmApiClient.TriageAsync(
            doc,
            rawFile,
            promptText,
            promptVersion,
            ct);
    }

    public async Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, CancellationToken ct) =>
        await ExtractAsync(doc, rawFile, _llmApiClient, ct);
    public async Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, ILlmApiClient llmClient, CancellationToken ct)
    {
        _logger.LogInformation("Extracting PDF document {DocumentId} via LLM", doc.Id);
        var (promptText, promptVersion) = _promptService.GetPrompt(PipelinePhase.Extraction, doc.DocumentType);
        return await llmClient.ExtractAsync(
            doc,
            rawFile,
            doc.DocumentType,
            promptText,
            promptVersion,
            ct);
    }
}
