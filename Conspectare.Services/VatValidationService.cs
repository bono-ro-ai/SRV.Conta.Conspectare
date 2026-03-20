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
                    ValidationError: "ANAF validation unavailable")));
            }
        }

        if (!string.IsNullOrWhiteSpace(customerCui))
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
                    ValidationError: "ANAF validation unavailable")));
            }
        }

        var hasIssues = validationResults.Any(r => !r.result.IsValid || r.result.IsInactive);

        if (hasIssues)
        {
            new SaveVatValidationResultCommand(document, validationResults).Execute();

            _logger.LogInformation(
                "VatValidationService: document {DocumentId} VAT validation completed with issues",
                document.Id);
        }
        else
        {
            new SaveVatValidationResultCommand(document, validationResults).Execute();

            _logger.LogInformation(
                "VatValidationService: document {DocumentId} VAT validation passed — all CUIs valid",
                document.Id);
        }
    }
}
