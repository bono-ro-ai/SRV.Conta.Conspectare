namespace Conspectare.Services.Validation;

public static class CuiValidator
{
    private static readonly int[] Weights = [7, 5, 3, 2, 1, 7, 5, 3, 2];

    public static (bool IsValid, string NormalizedCui, string Error) IsValidCui(string cui)
    {
        if (string.IsNullOrWhiteSpace(cui))
            return (false, null, "CUI is empty");

        var normalized = cui.Trim();
        if (normalized.StartsWith("RO", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[2..];

        if (normalized.Length < 2 || normalized.Length > 10)
            return (false, normalized, $"CUI '{cui}' has invalid length ({normalized.Length} digits) — Romanian CUIs must be 2-10 digits");

        if (!normalized.All(char.IsDigit))
            return (false, normalized, $"CUI '{cui}' is not a valid numeric identifier");

        var digits = new int[normalized.Length];
        for (var i = 0; i < normalized.Length; i++)
            digits[i] = normalized[i] - '0';

        var checkDigit = digits[^1];
        var weightOffset = Weights.Length - (digits.Length - 1);

        var sum = 0;
        for (var i = 0; i < digits.Length - 1; i++)
            sum += digits[i] * Weights[weightOffset + i];

        var remainder = (sum * 10) % 11;
        var expected = remainder == 10 ? 0 : remainder;

        if (checkDigit != expected)
            return (false, normalized, $"CUI '{cui}' has an invalid check digit (expected {expected}, got {checkDigit})");

        return (true, normalized, null);
    }
}
