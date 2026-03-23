using System.Xml.Linq;
using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services.Interfaces;
using Conspectare.Services.Models;
using Conspectare.Services.Processors.Models;

namespace Conspectare.Services.Processors;

public class EFacturaXmlProcessor : IDocumentProcessor
{
    private const string SchemaVersion = "1.0.0";
    private const string ModelId = "efactura_xml_parser";
    private const string PromptVersion = "n/a";

    private static readonly XNamespace UblInvoice =
        "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";

    private static readonly XNamespace UblCreditNote =
        "urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2";

    private static readonly XNamespace Cbc =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    private static readonly XNamespace Cac =
        "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";

    public bool CanProcess(string inputFormat, string contentType) =>
        inputFormat == InputFormat.XmlEfactura;

    public Task<TriageResult> TriageAsync(Document doc, Stream rawFile, CancellationToken ct)
    {
        XDocument xDoc;
        try
        {
            xDoc = XDocument.Load(rawFile);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Malformed XML: unable to parse document.", ex);
        }

        var root = xDoc.Root;
        if (root == null)
            throw new InvalidOperationException("Malformed XML: document has no root element.");

        var rootNs = root.Name.Namespace;

        if (rootNs == UblInvoice || rootNs == UblCreditNote)
        {
            return Task.FromResult(new TriageResult(
                DocumentType: DocumentType.Invoice,
                Confidence: 1.0m,
                IsAccountingRelevant: true,
                ModelId: ModelId,
                PromptVersion: PromptVersion,
                InputTokens: null,
                OutputTokens: null,
                LatencyMs: null));
        }

        return Task.FromResult(new TriageResult(
            DocumentType: DocumentType.Unknown,
            Confidence: 0.0m,
            IsAccountingRelevant: false,
            ModelId: ModelId,
            PromptVersion: PromptVersion,
            InputTokens: null,
            OutputTokens: null,
            LatencyMs: null));
    }

    public Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, CancellationToken ct) =>
        ExtractAsync(doc, rawFile, (ILlmApiClient)null, ct);
    public Task<ExtractionResult> ExtractAsync(Document doc, Stream rawFile, ILlmApiClient llmClient, CancellationToken ct)
    {
        var xDoc = XDocument.Load(rawFile);
        var root = xDoc.Root!;
        var isCreditNote = root.Name.Namespace == UblCreditNote;

        var reviewFlags = new List<ReviewFlagInfo>();

        var invoiceNumber = root.Element(Cbc + "ID")?.Value;
        var issueDate = root.Element(Cbc + "IssueDate")?.Value;
        var dueDate = root.Element(Cbc + "DueDate")?.Value;
        var currency = root.Element(Cbc + "DocumentCurrencyCode")?.Value;
        var notes = root.Element(Cbc + "Note")?.Value;

        ValidateMandatory(invoiceNumber, "invoice_number", "Invoice number (cbc:ID) is missing", reviewFlags);
        ValidateMandatory(issueDate, "issue_date", "Issue date (cbc:IssueDate) is missing", reviewFlags);
        ValidateMandatory(currency, "currency", "Currency (cbc:DocumentCurrencyCode) is missing", reviewFlags);

        var supplier = ParseParty(root.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party"));
        var customer = ParseParty(root.Element(Cac + "AccountingCustomerParty")?.Element(Cac + "Party"));

        var lineItems = ParseLineItems(root, isCreditNote);

        var legalMonetaryTotal = root.Element(Cac + "LegalMonetaryTotal");
        var totals = ParseTotals(legalMonetaryTotal);

        var lineTotalSum = lineItems.Sum(li => li.LineTotal);
        if (totals != null && Math.Abs(lineTotalSum - totals.Subtotal) > 0.01m)
        {
            reviewFlags.Add(new ReviewFlagInfo(
                "inconsistent_totals",
                ReviewFlagSeverity.Warning,
                $"Sum of line totals ({lineTotalSum:F2}) does not match declared subtotal ({totals.Subtotal:F2})"));
        }

        var paymentMeansCode = root.Element(Cac + "PaymentMeans")
            ?.Element(Cbc + "PaymentMeansCode")?.Value;
        var paymentMethod = paymentMeansCode != null ? MapPaymentCode(paymentMeansCode) : null;

        var documentType = isCreditNote ? DocumentType.CreditNote : DocumentType.Invoice;

        var canonical = new CanonicalInvoice(
            SchemaVersion: SchemaVersion,
            DocumentType: documentType,
            InvoiceNumber: invoiceNumber,
            IssueDate: issueDate,
            DueDate: dueDate,
            Currency: currency,
            Supplier: supplier,
            Customer: customer,
            LineItems: lineItems,
            Totals: totals,
            PaymentMethod: paymentMethod,
            Notes: notes,
            SupplierCui: supplier?.Cui,
            CustomerCui: customer?.Cui,
            TotalAmount: totals?.Total,
            VatAmount: totals?.VatAmount);

        var outputJson = canonical.ToJson();

        return Task.FromResult(new ExtractionResult(
            OutputJson: outputJson,
            SchemaVersion: SchemaVersion,
            ModelId: ModelId,
            PromptVersion: PromptVersion,
            InputTokens: null,
            OutputTokens: null,
            LatencyMs: null,
            ReviewFlags: reviewFlags));
    }

    private static void ValidateMandatory(
        string value, string fieldName, string message, List<ReviewFlagInfo> flags)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            flags.Add(new ReviewFlagInfo(
                $"missing_{fieldName}",
                ReviewFlagSeverity.Error,
                message));
        }
    }

    private static PartyInfo ParseParty(XElement partyElement)
    {
        if (partyElement == null)
            return null;

        var name = partyElement.Element(Cac + "PartyName")?.Element(Cbc + "Name")?.Value
                   ?? partyElement.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "RegistrationName")?.Value;

        var cui = ExtractCui(partyElement);
        var regCom = partyElement.Element(Cac + "PartyLegalEntity")?.Element(Cbc + "CompanyLegalForm")?.Value;
        var address = ParseAddress(partyElement.Element(Cac + "PostalAddress"));

        return new PartyInfo(name, cui, regCom, address);
    }

    private static AddressInfo ParseAddress(XElement addressElement)
    {
        if (addressElement == null)
            return null;

        var street = addressElement.Element(Cbc + "StreetName")?.Value;
        var city = addressElement.Element(Cbc + "CityName")?.Value;
        var county = addressElement.Element(Cbc + "CountrySubentity")?.Value;
        var country = addressElement.Element(Cac + "Country")?.Element(Cbc + "IdentificationCode")?.Value;

        return new AddressInfo(street, city, county, country);
    }

    private static List<CanonicalLineItem> ParseLineItems(XElement root, bool isCreditNote)
    {
        var lineElementName = isCreditNote ? "CreditNoteLine" : "InvoiceLine";
        var quantityElementName = isCreditNote ? "CreditedQuantity" : "InvoicedQuantity";

        var items = new List<CanonicalLineItem>();

        foreach (var line in root.Elements(Cac + lineElementName))
        {
            var description = line.Element(Cac + "Item")?.Element(Cbc + "Name")?.Value;

            var quantityEl = line.Element(Cbc + quantityElementName);
            var quantity = decimal.TryParse(quantityEl?.Value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var q) ? q : 0m;
            var unitOfMeasure = quantityEl?.Attribute("unitCode")?.Value;

            var priceAmount = line.Element(Cac + "Price")?.Element(Cbc + "PriceAmount")?.Value;
            var unitPrice = decimal.TryParse(priceAmount, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var up) ? up : 0m;

            var lineTotalStr = line.Element(Cbc + "LineExtensionAmount")?.Value;
            var lineTotal = decimal.TryParse(lineTotalStr, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var lt) ? lt : 0m;

            var vatPercent = line.Element(Cac + "Item")
                ?.Element(Cac + "ClassifiedTaxCategory")
                ?.Element(Cbc + "Percent")?.Value;
            var vatRate = decimal.TryParse(vatPercent, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var vr) ? vr : 0m;

            items.Add(new CanonicalLineItem(description, quantity, unitOfMeasure, unitPrice, vatRate, lineTotal));
        }

        return items;
    }

    private static InvoiceTotals ParseTotals(XElement legalMonetaryTotal)
    {
        if (legalMonetaryTotal == null)
            return null;

        var subtotalStr = legalMonetaryTotal.Element(Cbc + "LineExtensionAmount")?.Value;
        var subtotal = decimal.TryParse(subtotalStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 0m;

        var totalStr = legalMonetaryTotal.Element(Cbc + "PayableAmount")?.Value
                       ?? legalMonetaryTotal.Element(Cbc + "TaxInclusiveAmount")?.Value;
        var total = decimal.TryParse(totalStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0m;

        var vatAmount = total - subtotal;

        return new InvoiceTotals(subtotal, vatAmount, total);
    }

    private static string ExtractCui(XElement party)
    {
        var taxSchemeId = party.Element(Cac + "PartyTaxScheme")
            ?.Element(Cbc + "CompanyID")?.Value;

        if (!string.IsNullOrWhiteSpace(taxSchemeId))
            return taxSchemeId;

        return party.Element(Cac + "PartyLegalEntity")
            ?.Element(Cbc + "CompanyID")?.Value;
    }

    private static string MapPaymentCode(string code)
    {
        return code switch
        {
            "10" => "cash",
            "20" => "check",
            "30" => "transfer",
            "31" => "transfer",
            "42" => "bank_account",
            "48" => "card",
            "49" => "direct_debit",
            "57" => "standing_order",
            "58" => "sepa_transfer",
            "59" => "sepa_direct_debit",
            "97" => "clearing",
            _ => code
        };
    }
}
