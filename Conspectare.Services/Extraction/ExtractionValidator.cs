using Conspectare.Services.Models;
using Conspectare.Services.Processors.Models;

namespace Conspectare.Services.Extraction;

public static class ExtractionValidator
{
    /// <summary>Per-line rounding tolerance: allows ±2 bani (RON cents) for quantity × unit_price floating-point discrepancies.</summary>
    private const decimal LineItemTolerance = 0.02m;
    /// <summary>Invoice total tolerance: allows ±1 RON for cumulative rounding across line items, VAT, and discount.</summary>
    private const decimal TotalTolerance = 1.00m;

    public static List<ReviewFlagInfo> Validate(CanonicalInvoice invoice)
    {
        var findings = new List<ReviewFlagInfo>();
        ValidateRequiredFields(invoice, findings);
        ValidateLineItemMath(invoice, findings);
        ValidateTotalMath(invoice, findings);
        ValidateVatCoherence(invoice, findings);
        return findings;
    }

    private static void ValidateRequiredFields(CanonicalInvoice invoice, List<ReviewFlagInfo> findings)
    {
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNumber))
            findings.Add(new ReviewFlagInfo("missing_required_field", "error", "Invoice number is missing"));
        if (string.IsNullOrWhiteSpace(invoice.IssueDate))
            findings.Add(new ReviewFlagInfo("missing_required_field", "error", "Issue date is missing"));
        if (invoice.Supplier == null || string.IsNullOrWhiteSpace(invoice.Supplier.Name))
            findings.Add(new ReviewFlagInfo("missing_required_field", "error", "Supplier name is missing"));
        if (invoice.Supplier == null || string.IsNullOrWhiteSpace(invoice.Supplier.Cui))
            findings.Add(new ReviewFlagInfo("missing_required_field", "warning", "Supplier tax ID (CUI) is missing"));
    }

    private static void ValidateLineItemMath(CanonicalInvoice invoice, List<ReviewFlagInfo> findings)
    {
        if (invoice.LineItems == null) return;
        for (var i = 0; i < invoice.LineItems.Count; i++)
        {
            var item = invoice.LineItems[i];
            if (item.Quantity == 0 || item.UnitPrice == 0 || item.LineTotal == 0) continue;
            var expected = item.Quantity * item.UnitPrice;
            var diff = Math.Abs(expected - item.LineTotal);
            if (diff > LineItemTolerance)
            {
                findings.Add(new ReviewFlagInfo(
                    "line_item_math_mismatch",
                    "warning",
                    $"Line item {i + 1}: quantity ({item.Quantity}) × unit_price ({item.UnitPrice}) = {expected:F2}, but line_total is {item.LineTotal:F2} (diff: {diff:F2})"));
            }
        }
    }

    private static void ValidateTotalMath(CanonicalInvoice invoice, List<ReviewFlagInfo> findings)
    {
        if (invoice.Totals == null) return;
        if (invoice.Totals.TaxInclusiveAmount == 0) return;
        if (invoice.LineItems == null || invoice.LineItems.Count == 0) return;
        var lineSum = invoice.LineItems.Sum(li => li.LineTotal);
        var vatAmount = invoice.Totals.VatAmount;
        var discount = invoice.Discount ?? 0m;
        var expected = lineSum + vatAmount - discount;
        var diff = Math.Abs(expected - invoice.Totals.TaxInclusiveAmount);
        if (diff > TotalTolerance)
        {
            findings.Add(new ReviewFlagInfo(
                "total_math_mismatch",
                "warning",
                $"Sum of line totals ({lineSum:F2}) + VAT ({vatAmount:F2}) - discount ({discount:F2}) = {expected:F2}, but tax_inclusive_amount is {invoice.Totals.TaxInclusiveAmount:F2} (diff: {diff:F2})"));
        }
    }

    private static void ValidateVatCoherence(CanonicalInvoice invoice, List<ReviewFlagInfo> findings)
    {
        if (invoice.Totals == null) return;
        if (invoice.Totals.VatAmount > 0 && string.IsNullOrWhiteSpace(invoice.TaxCategory))
        {
            findings.Add(new ReviewFlagInfo(
                "missing_tax_category",
                "info",
                "VAT amount is present but tax_category is not specified"));
        }
    }
}
