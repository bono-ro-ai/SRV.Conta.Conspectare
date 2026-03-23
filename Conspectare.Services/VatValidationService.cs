using Conspectare.Domain.Entities;
using Conspectare.Services.Commands;
using Conspectare.Services.Core.Database;
using Conspectare.Services.ExternalIntegrations.Anaf;
using Conspectare.Services.Validation;
using Microsoft.Extensions.Logging;

namespace Conspectare.Services;

public class VatValidationService
{
    private readonly ILogger<VatValidationService> _logger;

    public VatValidationService(ILogger<VatValidationService> logger)
    {
        _logger = logger;
    }

    public Task ValidateDocumentAsync(Document document, CancellationToken ct)
    {
        var canonicalOutput = document.CanonicalOutput;
        if (canonicalOutput == null)
        {
            _logger.LogWarning(
                "VatValidationService: document {DocumentId} has no canonical output, skipping VAT validation",
                document.Id);
            return Task.CompletedTask;
        }

        var supplierCui = canonicalOutput.SupplierCui;
        var customerCui = canonicalOutput.CustomerCui;

        if (string.IsNullOrWhiteSpace(supplierCui) && string.IsNullOrWhiteSpace(customerCui))
        {
            _logger.LogInformation(
                "VatValidationService: document {DocumentId} has no CUIs in canonical output, skipping",
                document.Id);
            return Task.CompletedTask;
        }

        var validationResults = new List<(string role, AnafValidationResult result)>();

        if (!string.IsNullOrWhiteSpace(supplierCui))
        {
            var result = ValidateCui(supplierCui);
            validationResults.Add(("supplier", result));
        }

        if (!string.IsNullOrWhiteSpace(customerCui))
        {
            var result = ValidateCui(customerCui);
            validationResults.Add(("customer", result));
        }

        SaveValidationResults(document, validationResults);

        var hasIssues = validationResults.Any(r => !r.result.IsValid || r.result.IsInactive);
        _logger.LogInformation(
            hasIssues
                ? "VatValidationService: document {DocumentId} VAT validation completed with issues"
                : "VatValidationService: document {DocumentId} VAT validation passed — all CUIs valid",
            document.Id);

        return Task.CompletedTask;
    }

    private static AnafValidationResult ValidateCui(string cui)
    {
        var (isValid, normalizedCui, error) = CuiValidator.IsValidCui(cui);
        return new AnafValidationResult(
            IsValid: isValid,
            Cui: cui,
            CompanyName: null,
            IsInactive: false,
            ValidationError: error);
    }

    protected virtual void SaveValidationResults(
        Document document, IList<(string role, AnafValidationResult result)> validationResults)
    {
        new SaveVatValidationResultCommand(document, validationResults).Execute();
    }
}
