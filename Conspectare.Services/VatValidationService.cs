using Conspectare.Domain.Entities;
using Conspectare.Services.Commands;
using Conspectare.Services.Core.Database;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Microsoft.Extensions.Logging;

namespace Conspectare.Services;

public class VatValidationService
{
    private readonly IAnafVatValidationClient _anafClient;
    private readonly ILogger<VatValidationService> _logger;

    public VatValidationService(
        IAnafVatValidationClient anafClient,
        ILogger<VatValidationService> logger)
    {
        _anafClient = anafClient;
        _logger = logger;
    }

    public async Task ValidateDocumentAsync(Document document, CancellationToken ct)
    {
        var canonicalOutput = document.CanonicalOutput;
        if (canonicalOutput == null)
        {
            _logger.LogWarning(
                "VatValidationService: document {DocumentId} has no canonical output, skipping VAT validation",
                document.Id);
            return;
        }

        var supplierCui = canonicalOutput.SupplierCui;
        var customerCui = canonicalOutput.CustomerCui;

        if (string.IsNullOrWhiteSpace(supplierCui) && string.IsNullOrWhiteSpace(customerCui))
        {
            _logger.LogInformation(
                "VatValidationService: document {DocumentId} has no CUIs in canonical output, skipping",
                document.Id);
            return;
        }

        var validationResults = new List<(string role, AnafValidationResult result)>();

        if (!string.IsNullOrWhiteSpace(supplierCui))
        {
            if (IsRomanianCui(supplierCui))
            {
                try
                {
                    var result = await _anafClient.ValidateCuiAsync(supplierCui, ct);
                    validationResults.Add(("supplier", result));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "VatValidationService: ANAF validation failed for supplier CUI {Cui} on document {DocumentId}",
                        supplierCui, document.Id);
                    validationResults.Add(("supplier", new AnafValidationResult(
                        IsValid: false,
                        Cui: supplierCui,
                        CompanyName: null,
                        IsInactive: false,
                        ValidationError: "ANAF API request failed")));
                }
            }
            else
            {
                _logger.LogInformation(
                    "VatValidationService: skipping ANAF validation for non-Romanian supplier CUI {Cui} on document {DocumentId}",
                    supplierCui, document.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(customerCui))
        {
            if (IsRomanianCui(customerCui))
            {
                try
                {
                    var result = await _anafClient.ValidateCuiAsync(customerCui, ct);
                    validationResults.Add(("customer", result));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "VatValidationService: ANAF validation failed for customer CUI {Cui} on document {DocumentId}",
                        customerCui, document.Id);
                    validationResults.Add(("customer", new AnafValidationResult(
                        IsValid: false,
                        Cui: customerCui,
                        CompanyName: null,
                        IsInactive: false,
                        ValidationError: "ANAF API request failed")));
                }
            }
            else
            {
                _logger.LogInformation(
                    "VatValidationService: skipping ANAF validation for non-Romanian customer CUI {Cui} on document {DocumentId}",
                    customerCui, document.Id);
            }
        }

        SaveValidationResults(document, validationResults);

        var hasIssues = validationResults.Any(r => !r.result.IsValid || r.result.IsInactive);
        _logger.LogInformation(
            hasIssues
                ? "VatValidationService: document {DocumentId} VAT validation completed with issues"
                : "VatValidationService: document {DocumentId} VAT validation passed — all CUIs valid",
            document.Id);
    }

    private static bool IsRomanianCui(string cui)
    {
        var normalized = cui.Trim();
        if (normalized.StartsWith("RO", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];
        return normalized.Length >= 2 && normalized.Length <= 10 && normalized.All(char.IsDigit);
    }

    protected virtual void SaveValidationResults(
        Document document, IList<(string role, AnafValidationResult result)> validationResults)
    {
        new SaveVatValidationResultCommand(document, validationResults).Execute();
    }
}
