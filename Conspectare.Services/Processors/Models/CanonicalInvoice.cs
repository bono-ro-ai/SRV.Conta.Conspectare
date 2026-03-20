using System.Text.Json;
using System.Text.Json.Serialization;

namespace Conspectare.Services.Processors.Models;

public record CanonicalInvoice(
    string SchemaVersion,
    string DocumentType,
    string InvoiceNumber,
    string IssueDate,
    string DueDate,
    string Currency,
    PartyInfo Supplier,
    PartyInfo Customer,
    List<CanonicalLineItem> LineItems,
    InvoiceTotals Totals,
    string PaymentMethod,
    string Notes,
    string SupplierCui,
    string CustomerCui,
    decimal? TotalAmount,
    decimal? VatAmount,
    decimal? Discount = null,
    string TaxNote = null,
    string TaxCategory = null,
    string SwiftBic = null)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
}

public record PartyInfo(
    string Name,
    string Cui,
    string RegCom,
    AddressInfo Address);

public record AddressInfo(
    string Street,
    string City,
    string County,
    string Country);

public record CanonicalLineItem(
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal VatRate,
    decimal LineTotal);

public record InvoiceTotals(
    decimal TaxExclusiveAmount,
    decimal VatAmount,
    decimal TaxInclusiveAmount);
