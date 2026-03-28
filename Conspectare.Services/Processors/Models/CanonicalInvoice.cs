using System.Text.Json;
using System.Text.Json.Serialization;

namespace Conspectare.Services.Processors.Models;

/// <summary>
/// Canonical representation of an invoice or receipt after LLM extraction.
/// Provides a strongly-typed model that is serialised to JSON (snake_case, nulls omitted)
/// and stored as the canonical output for downstream consumers.
/// </summary>
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
    decimal? VatAmount)
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serialises this invoice to a compact JSON string using snake_case property names,
    /// omitting null fields.
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
}

/// <summary>
/// Identifies one party (supplier or customer) on an invoice.
/// </summary>
public record PartyInfo(
    string Name,
    string Cui,
    string RegCom,
    AddressInfo Address);

/// <summary>
/// Physical address of an invoice party.
/// </summary>
public record AddressInfo(
    string Street,
    string City,
    string County,
    string Country);

/// <summary>
/// A single line item on the invoice.
/// </summary>
public record CanonicalLineItem(
    string Description,
    decimal Quantity,
    string UnitOfMeasure,
    decimal UnitPrice,
    decimal VatRate,
    decimal LineTotal);

/// <summary>
/// Invoice-level monetary totals.
/// </summary>
public record InvoiceTotals(
    decimal Subtotal,
    decimal VatAmount,
    decimal Total);
