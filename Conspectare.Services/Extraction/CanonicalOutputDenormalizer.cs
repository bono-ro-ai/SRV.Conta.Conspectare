namespace Conspectare.Services.Extraction;
public static class CanonicalOutputDenormalizer
{
    public static void TryDenormalizeFields(Domain.Entities.CanonicalOutput output, string outputJson)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(outputJson);
            var root = doc.RootElement;
            if (root.TryGetProperty("invoice_number", out var inv))
                output.InvoiceNumber = inv.GetString();
            if (root.TryGetProperty("invoice_date", out var invoiceDate) &&
                DateTime.TryParse(invoiceDate.GetString(), out var parsedInvoiceDate))
                output.IssueDate = parsedInvoiceDate;
            else if (root.TryGetProperty("issue_date", out var issueDate) &&
                DateTime.TryParse(issueDate.GetString(), out var parsedIssueDate))
                output.IssueDate = parsedIssueDate;
            if (root.TryGetProperty("due_date", out var dueDate) &&
                DateTime.TryParse(dueDate.GetString(), out var parsedDueDate))
                output.DueDate = parsedDueDate;
            if (root.TryGetProperty("supplier", out var supplier) &&
                supplier.TryGetProperty("tax_id", out var sTaxId))
                output.SupplierCui = sTaxId.GetString();
            else if (root.TryGetProperty("supplier_cui", out var sCui))
                output.SupplierCui = sCui.GetString();
            if (root.TryGetProperty("customer", out var customer) &&
                customer.TryGetProperty("tax_id", out var cTaxId))
                output.CustomerCui = cTaxId.GetString();
            else if (root.TryGetProperty("customer_cui", out var cCui))
                output.CustomerCui = cCui.GetString();
            if (root.TryGetProperty("currency", out var currency))
                output.Currency = currency.GetString();
            if (root.TryGetProperty("tax_inclusive_amount", out var taxInclusive) &&
                taxInclusive.TryGetDecimal(out var taxInclusiveVal))
                output.TotalAmount = taxInclusiveVal;
            else if (root.TryGetProperty("total", out var total) &&
                total.TryGetDecimal(out var totalVal))
                output.TotalAmount = totalVal;
            else if (root.TryGetProperty("total_amount", out var totalAmount) &&
                totalAmount.TryGetDecimal(out var totalAmountVal))
                output.TotalAmount = totalAmountVal;
            if (root.TryGetProperty("total_vat", out var totalVat) &&
                totalVat.TryGetDecimal(out var totalVatVal))
                output.VatAmount = totalVatVal;
            else if (root.TryGetProperty("vat_amount", out var vat) &&
                vat.TryGetDecimal(out var vatVal))
                output.VatAmount = vatVal;
        }
        catch
        {
        }
    }
}
