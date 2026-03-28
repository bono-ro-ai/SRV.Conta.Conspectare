using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;
using Conspectare.Services.ExternalIntegrations.Anaf;

namespace Conspectare.Services.Commands;

public class ResolveVatFlagCommand(ReviewFlag flag, AnafValidationResult result) : NHibernateConspectareCommand
{
    /// <summary>
    /// Closes an open VAT review flag after a successful ANAF validation: merges
    /// the flag, marks it resolved, sets its severity to <c>Info</c>, and writes a
    /// human-readable confirmation message using the validated company details.
    /// </summary>
    protected override void OnExecute()
    {
        var merged = (ReviewFlag)Session.Merge(flag);

        merged.IsResolved = true;
        merged.ResolvedAt = DateTime.UtcNow;

        // Prefer the company name from ANAF; fall back to the raw CUI when unavailable.
        merged.Message = $"Validat: {result.CompanyName ?? result.Cui} — CUI activ în registrul ANAF";
        merged.Severity = ReviewFlagSeverity.Info;
    }
}
