using Conspectare.Domain.Entities;
using Conspectare.Services.Commands;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Conspectare.Workers;
public class VatRetryWorker : DistributedBackgroundService
{
    private const int BatchSize = 10;
    protected override string JobName => "vat_retry_worker";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);
    public VatRetryWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<VatRetryWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }
    protected override async Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VatRetryWorker>>();
        var anafClient = scope.ServiceProvider.GetRequiredService<IAnafVatValidationClient>();
        var failedFlags = new FindFailedVatFlagsQuery(BatchSize).Execute();
        if (failedFlags.Count == 0)
            return 0;
        var resolvedCount = 0;
        foreach (var flag in failedFlags)
        {
            var cui = ExtractCuiFromMessage(flag);
            if (string.IsNullOrEmpty(cui))
                continue;
            try
            {
                var result = await anafClient.ValidateCuiAsync(cui, ct);
                if (result.IsValid)
                {
                    new ResolveVatFlagCommand(flag, result).Execute();
                    resolvedCount++;
                    logger.LogInformation(
                        "VatRetryWorker: resolved flag {FlagId} — CUI {Cui} is valid ({CompanyName})",
                        flag.Id, cui, result.CompanyName);
                }
                else if (result.ValidationError != null && !result.ValidationError.Contains("API"))
                {
                    new UpdateVatFlagMessageCommand(flag, result.ValidationError).Execute();
                    logger.LogInformation(
                        "VatRetryWorker: updated flag {FlagId} — CUI {Cui}: {Error}",
                        flag.Id, cui, result.ValidationError);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "VatRetryWorker: retry failed for flag {FlagId}, CUI {Cui} — will retry later",
                    flag.Id, cui);
            }
        }
        return resolvedCount;
    }
    private static string ExtractCuiFromMessage(ReviewFlag flag)
    {
        var msg = flag.Message;
        if (string.IsNullOrEmpty(msg)) return null;
        var parts = msg.Split('\'');
        if (parts.Length >= 2) return parts[1];
        var colonIdx = msg.LastIndexOf(':');
        if (colonIdx >= 0 && colonIdx < msg.Length - 1)
            return msg[(colonIdx + 1)..].Trim();
        return null;
    }
}
