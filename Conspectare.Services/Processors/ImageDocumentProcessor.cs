using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Microsoft.Extensions.Logging;

namespace Conspectare.Services.Processors;

public class ImageDocumentProcessor : IDocumentProcessor
{
    private readonly ILlmApiClient _llmApiClient;
    private readonly IPromptService _promptService;
    private readonly ILogger<ImageDocumentProcessor> _logger;
    public ImageDocumentProcessor(ILlmApiClient llmApiClient, IPromptService promptService, ILogger<ImageDocumentProcessor> logger)
    {
        _llmApiClient = llmApiClient;
        _promptService = promptService;
        _logger = logger;
    }

    public bool CanProcess(string inputFormat, string contentType) =>
        inputFormat == InputFormat.Image;

    public async Task<TriageResult> TriageAsync(Document doc, Stream rawFile, CancellationToken ct)
    {
        _logger.LogInformation("Triaging image document {DocumentId} via Claude vision", doc.Id);

        var (promptText, promptVersion) = _promptService.GetPrompt("triage", null);
        return await _llmApiClient.TriageAsync(
            doc,
            rawFile,
            promptText,
            promptVersion,
            ct);
    }

    public async Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, CancellationToken ct)
    {
        _logger.LogInformation("Extracting image document {DocumentId} via Claude vision", doc.Id);

        var (promptText, promptVersion) = _promptService.GetPrompt("extraction", doc.DocumentType);
        return await _llmApiClient.ExtractAsync(
            doc,
            rawFile,
            doc.DocumentType,
            promptText,
            promptVersion,
            ct);
    }
}
