using Conspectare.Domain.Entities;
using Conspectare.Services.Commands;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Conspectare.Services.Validation;
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
    protected override Task<int> RunJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VatRetryWorker>>();
        var failedFlags = new FindFailedVatFlagsQuery(BatchSize).Execute();
        if (failedFlags.Count == 0)
            return Task.FromResult(0);
        var resolvedCount = 0;
        foreach (var flag in failedFlags)
        {
            var cui = ExtractCuiFromMessage(flag);
            if (string.IsNullOrEmpty(cui))
                continue;
            var (isValid, normalizedCui, error) = CuiValidator.IsValidCui(cui);
            var result = new AnafValidationResult(
                IsValid: isValid,
                Cui: cui,
                CompanyName: null,
                IsInactive: false,
                ValidationError: error);
            if (result.IsValid)
            {
                new ResolveVatFlagCommand(flag, result).Execute();
                resolvedCount++;
                logger.LogInformation(
                    "VatRetryWorker: resolved flag {FlagId} — CUI {Cui} is valid",
                    flag.Id, cui);
            }
            else if (result.ValidationError != null)
            {
                new UpdateVatFlagMessageCommand(flag, result.ValidationError).Execute();
                logger.LogInformation(
                    "VatRetryWorker: updated flag {FlagId} — CUI {Cui}: {Error}",
                    flag.Id, cui, result.ValidationError);
            }
        }
        return Task.FromResult(resolvedCount);
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
