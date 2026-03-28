using Conspectare.Domain.Entities;
using Conspectare.Services.Commands;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Queries;
using Conspectare.Services.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Conspectare.Workers;

/// <summary>
/// Periodically retries VAT validation for review flags that previously failed due to
/// transient errors or missing CUI data. Flags that pass local CUI validation are resolved;
/// those that still fail have their stored error message updated.
/// </summary>
public class VatRetryWorker : DistributedBackgroundService
{
    private const int BatchSize = 10;

    protected override string JobName => "vat_retry_worker";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    /// <summary>Initialises the worker with the required infrastructure dependencies.</summary>
    public VatRetryWorker(
        IDistributedLock distributedLock,
        IServiceScopeFactory scopeFactory,
        ILogger<VatRetryWorker> logger)
        : base(distributedLock, scopeFactory, logger)
    {
    }

    /// <summary>
    /// Loads a batch of failed VAT flags and attempts to resolve each one by re-running
    /// local CUI validation. Returns the number of flags successfully resolved.
    /// </summary>
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

            // Build a synthetic validation result from the local check so downstream
            // commands receive the same shape they would from a live ANAF call.
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

    /// <summary>
    /// Extracts the CUI value embedded in a <see cref="ReviewFlag.Message"/> string.
    /// Messages are expected to contain the CUI either between single-quotes (e.g. 'RO12345')
    /// or after the last colon (e.g. "Invalid CUI: RO12345").
    /// Returns <see langword="null"/> when no CUI can be parsed.
    /// </summary>
    private static string ExtractCuiFromMessage(ReviewFlag flag)
    {
        var msg = flag.Message;
        if (string.IsNullOrEmpty(msg))
            return null;

        // Primary format: CUI is the second token when the message is split on single-quotes.
        var parts = msg.Split('\'');
        if (parts.Length >= 2)
            return parts[1];

        // Fallback format: CUI follows the last colon in the message.
        var colonIdx = msg.LastIndexOf(':');
        if (colonIdx >= 0 && colonIdx < msg.Length - 1)
            return msg[(colonIdx + 1)..].Trim();

        return null;
    }
}
