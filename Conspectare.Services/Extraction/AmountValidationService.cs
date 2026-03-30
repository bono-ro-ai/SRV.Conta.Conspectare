using System.Text.Json;
using Conspectare.Domain.Enums;
using Conspectare.Services.Models;

namespace Conspectare.Services.Extraction;

/// <summary>
/// Server-side math validation of extracted amounts.
/// Parses the canonical output JSON and checks totals consistency.
/// Never modifies values — only produces review flags for mismatches.
/// </summary>
public static class AmountValidationService
{
    private const decimal TotalTolerance = 1.00m;
    private const decimal LineTolerance = 0.05m;

    public static List<ReviewFlagInfo> Validate(string outputJson)
    {
        var flags = new List<ReviewFlagInfo>();

        if (string.IsNullOrWhiteSpace(outputJson))
            return flags;

        JsonElement root;
        try
        {
            root = JsonDocument.Parse(outputJson).RootElement;
        }
        catch
        {
            return flags;
        }

        var taxExclusive = GetDecimal(root, "tax_exclusive_amount") ?? GetDecimal(root, "subtotal");
        var totalVat = GetDecimal(root, "total_vat");
        var taxInclusive = GetDecimal(root, "tax_inclusive_amount") ?? GetDecimal(root, "total");

        // Check: subtotal + total_vat ≈ total
        if (taxExclusive.HasValue && totalVat.HasValue && taxInclusive.HasValue)
        {
            var expected = taxExclusive.Value + totalVat.Value;
            var diff = Math.Abs(expected - taxInclusive.Value);
            if (diff > TotalTolerance)
            {
                flags.Add(new ReviewFlagInfo(
                    "calculation_mismatch",
                    ReviewFlagSeverity.Warning,
                    $"Server-side validation: subtotal ({taxExclusive.Value:F2}) + total_vat ({totalVat.Value:F2}) = {expected:F2}, " +
                    $"but tax_inclusive_amount is {taxInclusive.Value:F2} (diff={diff:F2}). Printed values kept as-is."));
            }
        }

        // Check: sum of line totals ≈ subtotal
        if (taxExclusive.HasValue && root.TryGetProperty("line_items", out var lineItems) && lineItems.ValueKind == JsonValueKind.Array)
        {
            var lineSum = 0m;
            var hasLineTotals = false;
            var lineIndex = 0;

            foreach (var item in lineItems.EnumerateArray())
            {
                var lineTotalWithoutVat = GetDecimal(item, "line_total_without_vat");
                if (lineTotalWithoutVat.HasValue)
                {
                    lineSum += lineTotalWithoutVat.Value;
                    hasLineTotals = true;
                }

                // Per-line: quantity × unit_price ≈ line_total_without_vat
                var quantity = GetDecimal(item, "quantity");
                var unitPrice = GetDecimal(item, "unit_price");
                if (quantity.HasValue && unitPrice.HasValue && lineTotalWithoutVat.HasValue)
                {
                    var expectedLineTotal = quantity.Value * unitPrice.Value;
                    var lineDiff = Math.Abs(expectedLineTotal - lineTotalWithoutVat.Value);
                    if (lineDiff > LineTolerance)
                    {
                        flags.Add(new ReviewFlagInfo(
                            "calculation_mismatch",
                            ReviewFlagSeverity.Warning,
                            $"Server-side validation: line_items[{lineIndex}] quantity ({quantity.Value}) × unit_price ({unitPrice.Value:F2}) = {expectedLineTotal:F2}, " +
                            $"but line_total_without_vat is {lineTotalWithoutVat.Value:F2} (diff={lineDiff:F2})."));
                    }
                }

                lineIndex++;
            }

            if (hasLineTotals)
            {
                var subtotalDiff = Math.Abs(lineSum - taxExclusive.Value);
                if (subtotalDiff > TotalTolerance)
                {
                    flags.Add(new ReviewFlagInfo(
                        "calculation_mismatch",
                        ReviewFlagSeverity.Warning,
                        $"Server-side validation: sum of line totals ({lineSum:F2}) does not match subtotal ({taxExclusive.Value:F2}), diff={subtotalDiff:F2}."));
                }
            }
        }

        return flags;
    }

    private static decimal? GetDecimal(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var value))
                return value;
        }
        return null;
    }
}
