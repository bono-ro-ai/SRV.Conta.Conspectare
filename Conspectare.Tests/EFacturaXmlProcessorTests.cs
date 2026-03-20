using System.Text;
using System.Text.Json;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Processors;
using Xunit;

namespace Conspectare.Tests;

public class EFacturaXmlProcessorTests
{
    private readonly EFacturaXmlProcessor _processor = new();

    private static readonly string ValidInvoiceXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
         xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2""
         xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"">
  <cbc:ID>FA-2026-001</cbc:ID>
  <cbc:IssueDate>2026-03-15</cbc:IssueDate>
  <cbc:DueDate>2026-04-15</cbc:DueDate>
  <cbc:InvoiceTypeCode>380</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>RON</cbc:DocumentCurrencyCode>
  <cbc:Note>Test invoice note</cbc:Note>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Furnizor SRL</cbc:Name></cac:PartyName>
      <cac:PartyLegalEntity>
        <cbc:CompanyID>RO12345678</cbc:CompanyID>
        <cbc:RegistrationName>SC Furnizor SRL</cbc:RegistrationName>
      </cac:PartyLegalEntity>
      <cac:PostalAddress>
        <cbc:StreetName>Str. Exemplu 1</cbc:StreetName>
        <cbc:CityName>Bucuresti</cbc:CityName>
        <cbc:CountrySubentity>Bucuresti</cbc:CountrySubentity>
        <cac:Country><cbc:IdentificationCode>RO</cbc:IdentificationCode></cac:Country>
      </cac:PostalAddress>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Client SRL</cbc:Name></cac:PartyName>
      <cac:PartyLegalEntity>
        <cbc:CompanyID>RO87654321</cbc:CompanyID>
      </cac:PartyLegalEntity>
      <cac:PostalAddress>
        <cbc:StreetName>Bd. Client 2</cbc:StreetName>
        <cbc:CityName>Cluj-Napoca</cbc:CityName>
        <cbc:CountrySubentity>Cluj</cbc:CountrySubentity>
        <cac:Country><cbc:IdentificationCode>RO</cbc:IdentificationCode></cac:Country>
      </cac:PostalAddress>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:PaymentMeans>
    <cbc:PaymentMeansCode>30</cbc:PaymentMeansCode>
  </cac:PaymentMeans>
  <cac:TaxTotal>
    <cbc:TaxAmount currencyID=""RON"">19.00</cbc:TaxAmount>
  </cac:TaxTotal>
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionAmount currencyID=""RON"">100.00</cbc:LineExtensionAmount>
    <cbc:TaxExclusiveAmount currencyID=""RON"">100.00</cbc:TaxExclusiveAmount>
    <cbc:TaxInclusiveAmount currencyID=""RON"">119.00</cbc:TaxInclusiveAmount>
    <cbc:PayableAmount currencyID=""RON"">119.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
  <cac:InvoiceLine>
    <cbc:ID>1</cbc:ID>
    <cbc:InvoicedQuantity unitCode=""C62"">2</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID=""RON"">100.00</cbc:LineExtensionAmount>
    <cac:Item>
      <cbc:Name>Servicii consultanta IT</cbc:Name>
      <cac:ClassifiedTaxCategory>
        <cbc:Percent>19</cbc:Percent>
      </cac:ClassifiedTaxCategory>
    </cac:Item>
    <cac:Price>
      <cbc:PriceAmount currencyID=""RON"">50.00</cbc:PriceAmount>
    </cac:Price>
  </cac:InvoiceLine>
</Invoice>";

    private static readonly string ValidCreditNoteXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<CreditNote xmlns=""urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2""
            xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2""
            xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"">
  <cbc:ID>CN-2026-001</cbc:ID>
  <cbc:IssueDate>2026-03-15</cbc:IssueDate>
  <cbc:DocumentCurrencyCode>RON</cbc:DocumentCurrencyCode>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Furnizor SRL</cbc:Name></cac:PartyName>
      <cac:PartyLegalEntity><cbc:CompanyID>RO12345678</cbc:CompanyID></cac:PartyLegalEntity>
      <cac:PostalAddress>
        <cbc:StreetName>Str. Exemplu 1</cbc:StreetName>
        <cbc:CityName>Bucuresti</cbc:CityName>
        <cbc:CountrySubentity>Bucuresti</cbc:CountrySubentity>
        <cac:Country><cbc:IdentificationCode>RO</cbc:IdentificationCode></cac:Country>
      </cac:PostalAddress>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Client SRL</cbc:Name></cac:PartyName>
      <cac:PartyLegalEntity><cbc:CompanyID>RO87654321</cbc:CompanyID></cac:PartyLegalEntity>
      <cac:PostalAddress>
        <cbc:StreetName>Bd. Client 2</cbc:StreetName>
        <cbc:CityName>Cluj-Napoca</cbc:CityName>
        <cbc:CountrySubentity>Cluj</cbc:CountrySubentity>
        <cac:Country><cbc:IdentificationCode>RO</cbc:IdentificationCode></cac:Country>
      </cac:PostalAddress>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionAmount currencyID=""RON"">50.00</cbc:LineExtensionAmount>
    <cbc:TaxInclusiveAmount currencyID=""RON"">59.50</cbc:TaxInclusiveAmount>
    <cbc:PayableAmount currencyID=""RON"">59.50</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
  <cac:CreditNoteLine>
    <cbc:ID>1</cbc:ID>
    <cbc:CreditedQuantity unitCode=""C62"">1</cbc:CreditedQuantity>
    <cbc:LineExtensionAmount currencyID=""RON"">50.00</cbc:LineExtensionAmount>
    <cac:Item>
      <cbc:Name>Retur marfa</cbc:Name>
      <cac:ClassifiedTaxCategory>
        <cbc:Percent>19</cbc:Percent>
      </cac:ClassifiedTaxCategory>
    </cac:Item>
    <cac:Price>
      <cbc:PriceAmount currencyID=""RON"">50.00</cbc:PriceAmount>
    </cac:Price>
  </cac:CreditNoteLine>
</CreditNote>";

    private static readonly string NonUblXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<root xmlns=""http://example.com/not-ubl"">
  <data>Not a UBL document</data>
</root>";

    private static readonly string MissingFieldsXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
         xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2""
         xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"">
  <cbc:DueDate>2026-04-15</cbc:DueDate>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Furnizor SRL</cbc:Name></cac:PartyName>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Client SRL</cbc:Name></cac:PartyName>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionAmount currencyID=""RON"">100.00</cbc:LineExtensionAmount>
    <cbc:PayableAmount currencyID=""RON"">119.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
</Invoice>";

    private static readonly string InconsistentTotalsXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<Invoice xmlns=""urn:oasis:names:specification:ubl:schema:xsd:Invoice-2""
         xmlns:cbc=""urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2""
         xmlns:cac=""urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"">
  <cbc:ID>FA-2026-002</cbc:ID>
  <cbc:IssueDate>2026-03-15</cbc:IssueDate>
  <cbc:DocumentCurrencyCode>RON</cbc:DocumentCurrencyCode>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Furnizor SRL</cbc:Name></cac:PartyName>
      <cac:PartyLegalEntity><cbc:CompanyID>RO12345678</cbc:CompanyID></cac:PartyLegalEntity>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Client SRL</cbc:Name></cac:PartyName>
      <cac:PartyLegalEntity><cbc:CompanyID>RO87654321</cbc:CompanyID></cac:PartyLegalEntity>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionAmount currencyID=""RON"">200.00</cbc:LineExtensionAmount>
    <cbc:PayableAmount currencyID=""RON"">238.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
  <cac:InvoiceLine>
    <cbc:ID>1</cbc:ID>
    <cbc:InvoicedQuantity unitCode=""C62"">1</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID=""RON"">100.00</cbc:LineExtensionAmount>
    <cac:Item>
      <cbc:Name>Servicii consultanta IT</cbc:Name>
      <cac:ClassifiedTaxCategory>
        <cbc:Percent>19</cbc:Percent>
      </cac:ClassifiedTaxCategory>
    </cac:Item>
    <cac:Price>
      <cbc:PriceAmount currencyID=""RON"">100.00</cbc:PriceAmount>
    </cac:Price>
  </cac:InvoiceLine>
</Invoice>";

    private static Document CreateTestDocument() => new()
    {
        Id = 1,
        TenantId = 100,
        InputFormat = InputFormat.XmlEfactura,
        ContentType = "application/xml",
        FileName = "test.xml"
    };

    private static Stream ToStream(string xml) =>
        new MemoryStream(Encoding.UTF8.GetBytes(xml));

    // --- CanProcess tests ---

    [Fact]
    public void CanProcess_XmlEfactura_ReturnsTrue()
    {
        Assert.True(_processor.CanProcess(InputFormat.XmlEfactura, "application/xml"));
    }

    [Fact]
    public void CanProcess_Pdf_ReturnsFalse()
    {
        Assert.False(_processor.CanProcess(InputFormat.Pdf, "application/pdf"));
    }

    // --- TriageAsync tests ---

    [Fact]
    public async Task TriageAsync_ValidInvoice_ReturnsInvoiceType()
    {
        using var stream = ToStream(ValidInvoiceXml);
        var result = await _processor.TriageAsync(CreateTestDocument(), stream, CancellationToken.None);

        Assert.Equal(DocumentType.Invoice, result.DocumentType);
        Assert.Equal(1.0m, result.Confidence);
        Assert.True(result.IsAccountingRelevant);
    }

    [Fact]
    public async Task TriageAsync_ValidCreditNote_ReturnsInvoiceType()
    {
        using var stream = ToStream(ValidCreditNoteXml);
        var result = await _processor.TriageAsync(CreateTestDocument(), stream, CancellationToken.None);

        Assert.Equal(DocumentType.Invoice, result.DocumentType);
        Assert.Equal(1.0m, result.Confidence);
        Assert.True(result.IsAccountingRelevant);
    }

    [Fact]
    public async Task TriageAsync_NonUblXml_ReturnsNotAccountingRelevant()
    {
        using var stream = ToStream(NonUblXml);
        var result = await _processor.TriageAsync(CreateTestDocument(), stream, CancellationToken.None);

        Assert.Equal(DocumentType.Unknown, result.DocumentType);
        Assert.Equal(0.0m, result.Confidence);
        Assert.False(result.IsAccountingRelevant);
    }

    [Fact]
    public async Task TriageAsync_MalformedXml_Throws()
    {
        using var stream = ToStream("this is not xml at all <<<<");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _processor.TriageAsync(CreateTestDocument(), stream, CancellationToken.None));

        Assert.Contains("Malformed XML", ex.Message);
    }

    // --- ExtractAsync tests ---

    [Fact]
    public async Task ExtractAsync_ValidInvoice_ProducesCanonicalJson()
    {
        using var stream = ToStream(ValidInvoiceXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        Assert.NotNull(result.OutputJson);
        Assert.Equal("2.0.0", result.SchemaVersion);
        Assert.Empty(result.ReviewFlags);

        var doc = JsonDocument.Parse(result.OutputJson);
        var root = doc.RootElement;

        Assert.Equal("FA-2026-001", root.GetProperty("invoice_number").GetString());
        Assert.Equal("2026-03-15", root.GetProperty("issue_date").GetString());
        Assert.Equal("2026-04-15", root.GetProperty("due_date").GetString());
        Assert.Equal("RON", root.GetProperty("currency").GetString());
        Assert.Equal("invoice", root.GetProperty("document_type").GetString());
        Assert.Equal("transfer", root.GetProperty("payment_method").GetString());
        Assert.Equal("Test invoice note", root.GetProperty("notes").GetString());
    }

    [Fact]
    public async Task ExtractAsync_ExtractsSupplierInfo()
    {
        using var stream = ToStream(ValidInvoiceXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        var doc = JsonDocument.Parse(result.OutputJson);
        var supplier = doc.RootElement.GetProperty("supplier");

        Assert.Equal("SC Furnizor SRL", supplier.GetProperty("name").GetString());
        Assert.Equal("RO12345678", supplier.GetProperty("cui").GetString());

        var address = supplier.GetProperty("address");
        Assert.Equal("Str. Exemplu 1", address.GetProperty("street").GetString());
        Assert.Equal("Bucuresti", address.GetProperty("city").GetString());
        Assert.Equal("Bucuresti", address.GetProperty("county").GetString());
        Assert.Equal("RO", address.GetProperty("country").GetString());

        Assert.Equal("RO12345678", doc.RootElement.GetProperty("supplier_cui").GetString());
    }

    [Fact]
    public async Task ExtractAsync_ExtractsCustomerInfo()
    {
        using var stream = ToStream(ValidInvoiceXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        var doc = JsonDocument.Parse(result.OutputJson);
        var customer = doc.RootElement.GetProperty("customer");

        Assert.Equal("SC Client SRL", customer.GetProperty("name").GetString());
        Assert.Equal("RO87654321", customer.GetProperty("cui").GetString());

        var address = customer.GetProperty("address");
        Assert.Equal("Bd. Client 2", address.GetProperty("street").GetString());
        Assert.Equal("Cluj-Napoca", address.GetProperty("city").GetString());
        Assert.Equal("Cluj", address.GetProperty("county").GetString());
        Assert.Equal("RO", address.GetProperty("country").GetString());

        Assert.Equal("RO87654321", doc.RootElement.GetProperty("customer_cui").GetString());
    }

    [Fact]
    public async Task ExtractAsync_ExtractsLineItems()
    {
        using var stream = ToStream(ValidInvoiceXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        var doc = JsonDocument.Parse(result.OutputJson);
        var lineItems = doc.RootElement.GetProperty("line_items");

        Assert.Equal(1, lineItems.GetArrayLength());

        var item = lineItems[0];
        Assert.Equal("Servicii consultanta IT", item.GetProperty("description").GetString());
        Assert.Equal(2m, item.GetProperty("quantity").GetDecimal());
        Assert.Equal("C62", item.GetProperty("unit_of_measure").GetString());
        Assert.Equal(50.00m, item.GetProperty("unit_price").GetDecimal());
        Assert.Equal(19m, item.GetProperty("vat_rate").GetDecimal());
        Assert.Equal(100.00m, item.GetProperty("line_total").GetDecimal());
    }

    [Fact]
    public async Task ExtractAsync_ExtractsCorrectTotals()
    {
        using var stream = ToStream(ValidInvoiceXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        var doc = JsonDocument.Parse(result.OutputJson);
        var totals = doc.RootElement.GetProperty("totals");

        Assert.Equal(100.00m, totals.GetProperty("tax_exclusive_amount").GetDecimal());
        Assert.Equal(19.00m, totals.GetProperty("vat_amount").GetDecimal());
        Assert.Equal(119.00m, totals.GetProperty("tax_inclusive_amount").GetDecimal());

        Assert.Equal(119.00m, doc.RootElement.GetProperty("total_amount").GetDecimal());
        Assert.Equal(19.00m, doc.RootElement.GetProperty("vat_amount").GetDecimal());
    }

    [Fact]
    public async Task ExtractAsync_MissingMandatoryField_CreatesReviewFlag()
    {
        using var stream = ToStream(MissingFieldsXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        Assert.True(result.ReviewFlags.Count >= 3);

        Assert.Contains(result.ReviewFlags, f =>
            f.FlagType == "missing_invoice_number" && f.Severity == "error");
        Assert.Contains(result.ReviewFlags, f =>
            f.FlagType == "missing_issue_date" && f.Severity == "error");
        Assert.Contains(result.ReviewFlags, f =>
            f.FlagType == "missing_currency" && f.Severity == "error");
    }

    [Fact]
    public async Task ExtractAsync_InconsistentTotals_CreatesWarningFlag()
    {
        using var stream = ToStream(InconsistentTotalsXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        Assert.Contains(result.ReviewFlags, f =>
            f.FlagType == "inconsistent_totals" && f.Severity == "warning");
    }

    [Fact]
    public async Task ExtractAsync_SnakeCaseJsonKeys()
    {
        using var stream = ToStream(ValidInvoiceXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        var doc = JsonDocument.Parse(result.OutputJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schema_version", out _));
        Assert.True(root.TryGetProperty("document_type", out _));
        Assert.True(root.TryGetProperty("invoice_number", out _));
        Assert.True(root.TryGetProperty("issue_date", out _));
        Assert.True(root.TryGetProperty("due_date", out _));
        Assert.True(root.TryGetProperty("supplier_cui", out _));
        Assert.True(root.TryGetProperty("customer_cui", out _));
        Assert.True(root.TryGetProperty("total_amount", out _));
        Assert.True(root.TryGetProperty("vat_amount", out _));
        Assert.True(root.TryGetProperty("line_items", out _));
        Assert.True(root.TryGetProperty("payment_method", out _));
    }

    [Fact]
    public async Task ExtractAsync_CreditNote_ExtractsCorrectly()
    {
        using var stream = ToStream(ValidCreditNoteXml);
        var result = await _processor.ExtractAsync(CreateTestDocument(), stream, CancellationToken.None);

        var doc = JsonDocument.Parse(result.OutputJson);
        var root = doc.RootElement;

        Assert.Equal("credit_note", root.GetProperty("document_type").GetString());
        Assert.Equal("CN-2026-001", root.GetProperty("invoice_number").GetString());
        Assert.Equal("RON", root.GetProperty("currency").GetString());

        var lineItems = root.GetProperty("line_items");
        Assert.Equal(1, lineItems.GetArrayLength());
        Assert.Equal("Retur marfa", lineItems[0].GetProperty("description").GetString());
        Assert.Equal(1m, lineItems[0].GetProperty("quantity").GetDecimal());

        var totals = root.GetProperty("totals");
        Assert.Equal(50.00m, totals.GetProperty("tax_exclusive_amount").GetDecimal());
        Assert.Equal(59.50m, totals.GetProperty("tax_inclusive_amount").GetDecimal());
    }
}
