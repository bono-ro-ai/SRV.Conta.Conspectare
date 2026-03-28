using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;
using Conspectare.Services.ExternalIntegrations.Anaf;

namespace Conspectare.Services.Commands;

public class SaveVatValidationResultCommand(
    Document document,
    IList<(string role, AnafValidationResult result)> validationResults)
    : NHibernateConspectareCommand
{
    /// <summary>
    /// Persists the outcome of ANAF VAT validation for a document's supplier and/or
    /// customer CUIs. For each failed validation a <see cref="ReviewFlag"/> is
    /// created: invalid CUIs raise an <c>invalid_*_cui</c> flag, active-but-inactive
    /// companies raise an <c>inactive_*_company</c> flag. A <c>VatValidationCompleted</c>
    /// audit event is always saved, summarising how many issues were found.
    /// </summary>
    protected override void OnExecute()
    {
        // Merge re-attaches the detached document snapshot passed in from the caller.
        var merged = (Document)Session.Merge(document);
        var utcNow = DateTime.UtcNow;
        var flagSummaries = new List<string>();

        foreach (var (role, result) in validationResults)
        {
            // Skip CUIs that are both valid and active — nothing to flag.
            if (result.IsValid && !result.IsInactive)
                continue;

            if (!result.IsValid)
            {
                // CUI could not be found in the ANAF registry at all.
                var flagType = role == "supplier"
                    ? "invalid_supplier_cui"
                    : "invalid_customer_cui";

                var flag = new ReviewFlag
                {
                    Document = merged,
                    DocumentId = merged.Id,
                    TenantId = merged.TenantId,
                    FlagType = flagType,
                    Severity = ReviewFlagSeverity.Warning,
                    Message = result.ValidationError ?? $"CUI '{result.Cui}' nu a fost validat în registrul ANAF",
                    IsResolved = false,
                    CreatedAt = utcNow
                };
                Session.Save(flag);
                flagSummaries.Add($"{flagType}: {result.Cui}");
            }
            else
            {
                // CUI exists in the registry but the company is currently inactive.
                var flagType = role == "supplier"
                    ? "inactive_supplier_company"
                    : "inactive_customer_company";

                var flag = new ReviewFlag
                {
                    Document = merged,
                    DocumentId = merged.Id,
                    TenantId = merged.TenantId,
                    FlagType = flagType,
                    Severity = ReviewFlagSeverity.Warning,
                    Message = $"Compania '{result.CompanyName ?? result.Cui}' (CUI: {result.Cui}) apare inactivă în registrul ANAF",
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
            EventType = DocumentEventType.VatValidationCompleted,
            Details = details,
            CreatedAt = utcNow
        };
        Session.Save(docEvent);
    }
}
