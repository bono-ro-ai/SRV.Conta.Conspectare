using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using Conspectare.Services.ExternalIntegrations.Anaf;

namespace Conspectare.Services.Commands;

public class ResolveVatFlagCommand(ReviewFlag flag, AnafValidationResult result) : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        var merged = (ReviewFlag)Session.Merge(flag);
        merged.IsResolved = true;
        merged.ResolvedAt = DateTime.UtcNow;
        merged.Message = $"Validat: {result.CompanyName ?? result.Cui} — CUI activ în registrul ANAF";
        merged.Severity = "info";
    }
}
