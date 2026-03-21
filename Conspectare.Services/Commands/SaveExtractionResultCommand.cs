using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class SaveExtractionResultCommand(
    Document document,
    CanonicalOutput canonicalOutput,
    ExtractionAttempt attempt,
    DocumentEvent statusEvent,
    DocumentArtifact artifact,
    IList<ReviewFlag> reviewFlags)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        var merged = (Document)Session.Merge(document);

        var existingOutput = Session.QueryOver<CanonicalOutput>()
            .Where(c => c.DocumentId == merged.Id)
            .SingleOrDefault();
        if (existingOutput != null)
        {
            existingOutput.SchemaVersion = canonicalOutput.SchemaVersion;
            existingOutput.OutputJson = canonicalOutput.OutputJson;
            existingOutput.InvoiceNumber = canonicalOutput.InvoiceNumber;
            existingOutput.IssueDate = canonicalOutput.IssueDate;
            existingOutput.DueDate = canonicalOutput.DueDate;
            existingOutput.SupplierCui = canonicalOutput.SupplierCui;
            existingOutput.CustomerCui = canonicalOutput.CustomerCui;
            existingOutput.Currency = canonicalOutput.Currency;
            existingOutput.TotalAmount = canonicalOutput.TotalAmount;
            existingOutput.VatAmount = canonicalOutput.VatAmount;
            existingOutput.ConsensusStrategy = canonicalOutput.ConsensusStrategy;
            existingOutput.WinningModelId = canonicalOutput.WinningModelId;
            existingOutput.CreatedAt = canonicalOutput.CreatedAt;
            Session.Update(existingOutput);
        }
        else
        {
            canonicalOutput.Document = merged;
            canonicalOutput.DocumentId = merged.Id;
            Session.Save(canonicalOutput);
        }

        if (artifact != null)
        {
            artifact.Document = merged;
            artifact.DocumentId = merged.Id;
            Session.Save(artifact);

            attempt.ResponseArtifactId = artifact.Id;
        }

        attempt.Document = merged;
        attempt.DocumentId = merged.Id;
        Session.Save(attempt);

        if (reviewFlags is { Count: > 0 })
        {
            foreach (var flag in reviewFlags)
            {
                flag.Document = merged;
                flag.DocumentId = merged.Id;
                Session.Save(flag);
            }
        }

        statusEvent.Document = merged;
        statusEvent.DocumentId = merged.Id;
        Session.Save(statusEvent);
    }
}
