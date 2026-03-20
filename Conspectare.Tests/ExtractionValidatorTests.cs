using Conspectare.Services.Extraction;
using Conspectare.Services.Processors.Models;
using Xunit;

namespace Conspectare.Tests;

public class ExtractionValidatorTests
{
    private static CanonicalInvoice BuildValidInvoice(
        decimal taxExclusiveAmount = 1000m,
        decimal vatAmount = 190m,
        decimal taxInclusiveAmount = 1190m,
        decimal? discount = null,
        string taxCategory = "S",
        string taxNote = null,
        List<CanonicalLineItem> lineItems = null)
    {
        lineItems ??= new List<CanonicalLineItem>
        {
            new("Service A", 2, "buc", 500, 19, 1000)
        };
        return new CanonicalInvoice(
            SchemaVersion: "2.0.0",
            DocumentType: "invoice",
            InvoiceNumber: "FA-2026-001",
            IssueDate: "2026-03-15",
            DueDate: "2026-04-15",
            Currency: "RON",
            Supplier: new PartyInfo("SC Furnizor SRL", "RO12345678", "J40/1234/2020",
                new AddressInfo("Str. Exemplu 1", "Bucuresti", "Bucuresti", "RO")),
            Customer: new PartyInfo("SC Client SRL", "RO87654321", null,
                new AddressInfo("Bd. Client 2", "Cluj-Napoca", "Cluj", "RO")),
            LineItems: lineItems,
            Totals: new InvoiceTotals(taxExclusiveAmount, vatAmount, taxInclusiveAmount),
            PaymentMethod: "transfer",
            Notes: null,
            SupplierCui: "RO12345678",
            CustomerCui: "RO87654321",
            TotalAmount: taxInclusiveAmount,
            VatAmount: vatAmount,
            Discount: discount,
            TaxNote: taxNote,
            TaxCategory: taxCategory);
    }

    [Fact]
    public void Validate_ValidInvoice_ReturnsNoFindings()
    {
        var invoice = BuildValidInvoice();
        var findings = ExtractionValidator.Validate(invoice);
        Assert.Empty(findings);
    }

    [Fact]
    public void Validate_MissingInvoiceNumber_ReturnsMissingRequiredField()
    {
        var invoice = BuildValidInvoice() with { InvoiceNumber = null };
        var findings = ExtractionValidator.Validate(invoice);
        Assert.Contains(findings, f => f.FlagType == "missing_required_field" && f.Message.Contains("Invoice number"));
    }

    [Fact]
    public void Validate_MissingIssueDate_ReturnsMissingRequiredField()
    {
        var invoice = BuildValidInvoice() with { IssueDate = "" };
        var findings = ExtractionValidator.Validate(invoice);
        Assert.Contains(findings, f => f.FlagType == "missing_required_field" && f.Message.Contains("Issue date"));
    }

    [Fact]
    public void Validate_MissingSupplierName_ReturnsMissingRequiredField()
    {
        var invoice = BuildValidInvoice() with
        {
            Supplier = new PartyInfo(null, "RO12345678", null, null)
        };
        var findings = ExtractionValidator.Validate(invoice);
        Assert.Contains(findings, f => f.FlagType == "missing_required_field" && f.Message.Contains("Supplier name"));
    }

    [Fact]
    public void Validate_MissingSupplierCui_ReturnsWarning()
    {
        var invoice = BuildValidInvoice() with
        {
            Supplier = new PartyInfo("SC Furnizor SRL", null, null, null)
        };
        var findings = ExtractionValidator.Validate(invoice);
        Assert.Contains(findings, f => f.FlagType == "missing_required_field" && f.Severity == "warning" && f.Message.Contains("tax ID"));
    }

    [Fact]
    public void Validate_LineItemMathMismatch_ReturnsWarning()
    {
        var items = new List<CanonicalLineItem>
        {
            new("Service A", 2, "buc", 500, 19, 1100)
        };
        var invoice = BuildValidInvoice(lineItems: items);
        var findings = ExtractionValidator.Validate(invoice);
        Assert.Contains(findings, f => f.FlagType == "line_item_math_mismatch");
    }

    [Fact]
    public void Validate_LineItemMathWithinTolerance_NoFinding()
    {
        var items = new List<CanonicalLineItem>
        {
            new("Service A", 3, "buc", 33.33m, 19, 99.99m)
        };
        var invoice = BuildValidInvoice(
            taxExclusiveAmount: 99.99m,
            vatAmount: 19m,
            taxInclusiveAmount: 118.99m,
            lineItems: items);
        var findings = ExtractionValidator.Validate(invoice);
        Assert.DoesNotContain(findings, f => f.FlagType == "line_item_math_mismatch");
    }

    [Fact]
    public void Validate_TotalMathMismatch_ReturnsWarning()
    {
        var items = new List<CanonicalLineItem>
        {
            new("Service A", 2, "buc", 500, 19, 1000)
        };
        var invoice = BuildValidInvoice(
            taxExclusiveAmount: 1000m,
            vatAmount: 190m,
            taxInclusiveAmount: 1500m,
            lineItems: items);
        var findings = ExtractionValidator.Validate(invoice);
        Assert.Contains(findings, f => f.FlagType == "total_math_mismatch");
    }

    [Fact]
    public void Validate_TotalMathWithDiscount_NoFinding()
    {
        var items = new List<CanonicalLineItem>
        {
            new("Service A", 2, "buc", 500, 19, 1000)
        };
        var invoice = BuildValidInvoice(
            taxExclusiveAmount: 900m,
            vatAmount: 190m,
            taxInclusiveAmount: 1090m,
            discount: 100m,
            lineItems: items);
        var findings = ExtractionValidator.Validate(invoice);
        Assert.DoesNotContain(findings, f => f.FlagType == "total_math_mismatch");
    }

    [Fact]
    public void Validate_TotalMathWithinTolerance_NoFinding()
    {
        var items = new List<CanonicalLineItem>
        {
            new("Service A", 2, "buc", 500, 19, 1000)
        };
        var invoice = BuildValidInvoice(
            taxExclusiveAmount: 1000m,
            vatAmount: 190m,
            taxInclusiveAmount: 1190.50m,
            lineItems: items);
        var findings = ExtractionValidator.Validate(invoice);
        Assert.DoesNotContain(findings, f => f.FlagType == "total_math_mismatch");
    }

    [Fact]
    public void Validate_VatPresentWithoutTaxCategory_ReturnsInfo()
    {
        var invoice = BuildValidInvoice(taxCategory: null);
        var findings = ExtractionValidator.Validate(invoice);
        Assert.Contains(findings, f => f.FlagType == "missing_tax_category" && f.Severity == "info");
    }

    [Fact]
    public void Validate_VatPresentWithTaxCategory_NoFinding()
    {
        var invoice = BuildValidInvoice(taxCategory: "S");
        var findings = ExtractionValidator.Validate(invoice);
        Assert.DoesNotContain(findings, f => f.FlagType == "missing_tax_category");
    }

    [Fact]
    public void Validate_NullTotals_NoFindings()
    {
        var invoice = BuildValidInvoice() with { Totals = null };
        var findings = ExtractionValidator.Validate(invoice);
        Assert.DoesNotContain(findings, f => f.FlagType == "total_math_mismatch");
        Assert.DoesNotContain(findings, f => f.FlagType == "missing_tax_category");
    }
}
