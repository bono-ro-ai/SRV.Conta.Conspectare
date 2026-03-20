using System.Collections.Concurrent;
using Conspectare.Domain.Entities;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Processors;
using Conspectare.Services.Queries;
using Microsoft.Extensions.Logging;

namespace Conspectare.Services;

public class PromptService : IPromptService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, (IList<PromptVersion> Versions, DateTime LoadedAt)> _cache = new();
    private readonly ILogger<PromptService> _logger;
    private readonly Random _random = new();

    public PromptService(ILogger<PromptService> logger)
    {
        _logger = logger;
    }

    public (string PromptText, string Version) GetPrompt(string phase, string documentType)
    {
        var cacheKey = $"{phase}:{documentType ?? ""}";

        var versions = GetCachedVersions(cacheKey, phase, documentType);

        if (versions.Count > 0)
        {
            var selected = SelectByWeight(versions);
            _logger.LogDebug(
                "Selected prompt version {Version} for phase={Phase} documentType={DocumentType}",
                selected.Version, phase, documentType);
            return (selected.PromptText, selected.Version);
        }

        _logger.LogDebug(
            "No active DB prompt versions for phase={Phase} documentType={DocumentType}, falling back to embedded",
            phase, documentType);
        return GetFallback(phase, documentType);
    }

    private IList<PromptVersion> GetCachedVersions(string cacheKey, string phase, string documentType)
    {
        if (_cache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow - cached.LoadedAt < CacheTtl)
            return cached.Versions;

        try
        {
            var versions = new FindActivePromptVersionsQuery(phase, documentType).Execute();
            _cache[cacheKey] = (versions, DateTime.UtcNow);
            return versions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load prompt versions from DB for {CacheKey}, using fallback", cacheKey);
            return Array.Empty<PromptVersion>();
        }
    }

    private PromptVersion SelectByWeight(IList<PromptVersion> versions)
    {
        if (versions.Count == 1)
            return versions[0];

        var totalWeight = 0;
        for (var i = 0; i < versions.Count; i++)
            totalWeight += versions[i].TrafficWeight;

        var roll = _random.Next(totalWeight);
        var cumulative = 0;

        for (var i = 0; i < versions.Count; i++)
        {
            cumulative += versions[i].TrafficWeight;
            if (roll < cumulative)
                return versions[i];
        }

        return versions[versions.Count - 1];
    }

    private static (string PromptText, string Version) GetFallback(string phase, string documentType)
    {
        return phase switch
        {
            "triage" => (PromptProvider.GetTriagePrompt(), PromptProvider.GetTriagePromptVersion()),
            "extraction" => (PromptProvider.GetExtractionPrompt(documentType), PromptProvider.GetExtractionPromptVersion(documentType)),
            _ => (PromptProvider.GetTriagePrompt(), PromptProvider.GetTriagePromptVersion())
        };
    }
}
