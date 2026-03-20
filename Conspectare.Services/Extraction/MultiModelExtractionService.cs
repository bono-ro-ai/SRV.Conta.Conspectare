using Conspectare.Domain.Entities;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Conspectare.Services.Extraction;
public class MultiModelExtractionService
{
    private readonly ILlmClientFactory _llmClientFactory;
    private readonly IProcessorRegistry _processorRegistry;
    private readonly IConsensusStrategy _consensusStrategy;
    private readonly IPromptService _promptService;
    private readonly MultiModelSettings _settings;
    private readonly ILogger<MultiModelExtractionService> _logger;
    public MultiModelExtractionService(
        ILlmClientFactory llmClientFactory,
        IProcessorRegistry processorRegistry,
        IConsensusStrategy consensusStrategy,
        IPromptService promptService,
        IOptions<MultiModelSettings> settings,
        ILogger<MultiModelExtractionService> logger)
    {
        _llmClientFactory = llmClientFactory;
        _processorRegistry = processorRegistry;
        _consensusStrategy = consensusStrategy;
        _promptService = promptService;
        _settings = settings.Value;
        _logger = logger;
    }
    public async Task<ConsensusResult> ExtractAsync(Document doc, byte[] rawFileBytes, CancellationToken ct)
    {
        var processor = _processorRegistry.Resolve(doc.InputFormat, doc.ContentType);
        var providers = _settings.Providers;
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
        var tasks = providers.Select(async providerKey =>
        {
            try
            {
                var llmClient = _llmClientFactory.GetClient(providerKey);
                using var stream = new MemoryStream(rawFileBytes);
                var result = await processor.ExtractAsync(doc, stream, llmClient, timeoutCts.Token);
                _logger.LogInformation(
                    "MultiModel: provider {Provider} completed extraction for document {DocumentId} in {LatencyMs}ms",
                    providerKey, doc.Id, result.LatencyMs);
                return (ProviderKey: providerKey, Result: result, Error: (Exception)null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "MultiModel: provider {Provider} failed extraction for document {DocumentId}",
                    providerKey, doc.Id);
                return (ProviderKey: providerKey, Result: (ExtractionResult)null, Error: ex);
            }
        }).ToList();
        var outcomes = await Task.WhenAll(tasks);
        var successful = outcomes
            .Where(o => o.Result != null)
            .Select(o => (o.ProviderKey, o.Result))
            .ToList();
        if (successful.Count == 0)
        {
            var firstError = outcomes.First().Error;
            throw new AggregateException(
                "All LLM providers failed during multi-model extraction.",
                outcomes.Select(o => o.Error).Where(e => e != null));
        }
        _logger.LogInformation(
            "MultiModel: {SuccessCount}/{TotalCount} providers succeeded for document {DocumentId}",
            successful.Count, providers.Count, doc.Id);
        return _consensusStrategy.Resolve(successful);
    }
}
