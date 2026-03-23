using System.Text.Json;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class UpdateCanonicalOutputCommand(
    Document document,
    string canonicalOutputJson,
    string outputJsonS3Key,
    DateTime utcNow)
    : NHibernateConspectareCommand
{
    protected override void OnExecute()
    {
        document.CanonicalOutput.OutputJson = canonicalOutputJson;
        document.CanonicalOutput.OutputJsonS3Key = outputJsonS3Key;

        try
        {
            using var jsonDoc = JsonDocument.Parse(canonicalOutputJson);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("invoiceNumber", out var inv))
                document.CanonicalOutput.InvoiceNumber = inv.GetString();
            if (root.TryGetProperty("issueDate", out var issued) && DateTime.TryParse(issued.GetString(), out var issueDateVal))
                document.CanonicalOutput.IssueDate = issueDateVal;
            if (root.TryGetProperty("dueDate", out var due) && DateTime.TryParse(due.GetString(), out var dueDateVal))
                document.CanonicalOutput.DueDate = dueDateVal;
            if (root.TryGetProperty("supplierCui", out var sCui))
                document.CanonicalOutput.SupplierCui = sCui.GetString();
            if (root.TryGetProperty("customerCui", out var cCui))
                document.CanonicalOutput.CustomerCui = cCui.GetString();
            if (root.TryGetProperty("currency", out var cur))
                document.CanonicalOutput.Currency = cur.GetString();
            if (root.TryGetProperty("totalAmount", out var total) && total.TryGetDecimal(out var totalVal))
                document.CanonicalOutput.TotalAmount = totalVal;
            if (root.TryGetProperty("vatAmount", out var vat) && vat.TryGetDecimal(out var vatVal))
                document.CanonicalOutput.VatAmount = vatVal;
        }
        catch (JsonException)
        {
        }

        Session.Merge(document.CanonicalOutput);

        document.UpdatedAt = utcNow;
        Session.Merge(document);

        var editEvent = new DocumentEvent
        {
            TenantId = document.TenantId,
            DocumentId = document.Id,
            Document = document,
            EventType = DocumentEventType.CanonicalOutputEdited,
            FromStatus = document.Status,
            ToStatus = document.Status,
            Details = "Canonical output edited via review UI",
            CreatedAt = utcNow
        };
        Session.Save(editEvent);
    }
}
