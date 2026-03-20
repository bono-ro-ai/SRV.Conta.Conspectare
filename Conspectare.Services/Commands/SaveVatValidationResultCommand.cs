using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using Conspectare.Services.ExternalIntegrations.Anaf;

namespace Conspectare.Services.Commands;

public class SaveVatValidationResultCommand(
    Document document,
    IList<(string role, AnafValidationResult result)> validationResults)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        var merged = (Document)Session.Merge(document);
        var utcNow = DateTime.UtcNow;
        var flagSummaries = new List<string>();

        foreach (var (role, result) in validationResults)
        {
            if (result.IsValid && !result.IsInactive)
                continue;

            if (!result.IsValid)
            {
                var flagType = role == "supplier"
                    ? "invalid_supplier_cui"
                    : "invalid_customer_cui";

                var flag = new ReviewFlag
                {
                    Document = merged,
                    DocumentId = merged.Id,
                    TenantId = merged.TenantId,
                    FlagType = flagType,
                    Severity = "high",
                    Message = result.ValidationError ?? $"CUI '{result.Cui}' is not valid in ANAF registry",
                    IsResolved = false,
                    CreatedAt = utcNow
                };
                Session.Save(flag);
                flagSummaries.Add($"{flagType}: {result.Cui}");
            }

            else
            {
                var flagType = role == "supplier"
                    ? "inactive_supplier_company"
                    : "inactive_customer_company";

                var flag = new ReviewFlag
                {
                    Document = merged,
                    DocumentId = merged.Id,
                    TenantId = merged.TenantId,
                    FlagType = flagType,
                    Severity = "medium",
                    Message = $"Company '{result.CompanyName ?? result.Cui}' (CUI: {result.Cui}) is marked as inactive in ANAF registry",
                    IsResolved = false,
                    CreatedAt = utcNow
                };
                Session.Save(flag);
                flagSummaries.Add($"{flagType}: {result.Cui}");
            }
        }

        var details = flagSummaries.Count > 0
            ? $"VAT validation completed with {flagSummaries.Count} issue(s): {string.Join(", ", flagSummaries)}"
            : "VAT validation completed — all CUIs valid and active";

        var docEvent = new DocumentEvent
        {
            Document = merged,
            DocumentId = merged.Id,
            TenantId = merged.TenantId,
            EventType = "vat_validation_completed",
            Details = details,
            CreatedAt = utcNow
        };
        Session.Save(docEvent);
    }
}
