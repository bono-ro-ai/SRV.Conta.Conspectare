using Conspectare.Services.Validation;
using Xunit;

namespace Conspectare.Tests;

public class CuiValidatorTests
{
    [Theory]
    [InlineData("16393852")]
    [InlineData("RO16393852")]
    [InlineData("ro16393852")]
    [InlineData("  RO16393852  ")]
    public void IsValidCui_KnownValidCui_ReturnsValid(string cui)
    {
        var (isValid, normalizedCui, error) = CuiValidator.IsValidCui(cui);

        Assert.True(isValid);
        Assert.Equal("16393852", normalizedCui);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("14399840")]
    [InlineData("RO14399840")]
    public void IsValidCui_AnotherKnownValidCui_ReturnsValid(string cui)
    {
        var (isValid, _, error) = CuiValidator.IsValidCui(cui);

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsValidCui_TwoDigitValidCui_ReturnsValid()
    {
        // CUI "10": digits=[1,0], weight offset = 9-1 = 8, so weight index 8 = 2
        // sum = 1*2 = 2, remainder = (2*10) % 11 = 20 % 11 = 9, check digit should be 9 -> "10" has check digit 0, invalid
        // Let's use a 2-digit CUI where check works: try "19"
        // sum = 1*2 = 2, remainder = 20 % 11 = 9. Check digit = 9 -> "19" is valid
        var (isValid, _, error) = CuiValidator.IsValidCui("19");

        Assert.True(isValid);
        Assert.Null(error);
    }

    [Fact]
    public void IsValidCui_InvalidCheckDigit_ReturnsInvalid()
    {
        var (isValid, _, error) = CuiValidator.IsValidCui("16393851");

        Assert.False(isValid);
        Assert.Contains("invalid check digit", error);
    }

    [Fact]
    public void IsValidCui_WithRoPrefixInvalidCheckDigit_ReturnsInvalid()
    {
        var (isValid, _, error) = CuiValidator.IsValidCui("RO16393851");

        Assert.False(isValid);
        Assert.Contains("invalid check digit", error);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("RO1")]
    public void IsValidCui_TooShort_ReturnsInvalid(string cui)
    {
        var (isValid, _, error) = CuiValidator.IsValidCui(cui);

        Assert.False(isValid);
        Assert.Contains("invalid length", error);
    }

    [Theory]
    [InlineData("12345678901")]
    [InlineData("RO12345678901")]
    public void IsValidCui_TooLong_ReturnsInvalid(string cui)
    {
        var (isValid, _, error) = CuiValidator.IsValidCui(cui);

        Assert.False(isValid);
        Assert.Contains("invalid length", error);
    }

    [Theory]
    [InlineData("ABCDEF")]
    [InlineData("12AB56")]
    [InlineData("RO12AB56")]
    public void IsValidCui_NonNumeric_ReturnsInvalid(string cui)
    {
        var (isValid, _, error) = CuiValidator.IsValidCui(cui);

        Assert.False(isValid);
        Assert.Contains("not a valid numeric", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidCui_NullOrEmptyOrWhitespace_ReturnsInvalid(string cui)
    {
        var (isValid, _, error) = CuiValidator.IsValidCui(cui);

        Assert.False(isValid);
        Assert.Equal("CUI is empty", error);
    }

    [Fact]
    public void IsValidCui_Remainder10BecomesZero_CheckDigitZero()
    {
        // Build a CUI where remainder = 10, so expected check digit = 0
        // 8-digit CUI: weights for 7 digits = [3,2,1,7,5,3,2]
        // Try: 1185240 + check
        // sum = 1*3 + 1*2 + 8*1 + 5*7 + 2*5 + 4*3 + 0*2 = 3+2+8+35+10+12+0 = 70
        // remainder = (70*10)%11 = 700%11 = 700 - 63*11 = 700-693 = 7 -> not 10
        // Try systematically: we need sum*10 % 11 = 10, so sum % 11 = 1
        // Use 7 digits with weights [3,2,1,7,5,3,2]: need sum ≡ 1 (mod 11)
        // 1000000: sum = 1*3 = 3. Need 1 mod 11. Diff = -2 mod 11 = 9. Add 9 via last weight (2): digit = 9/2 nope.
        // Try: 1000045: sum = 1*3+0*2+0*1+0*7+0*5+4*3+5*2 = 3+12+10 = 25. 25%11=3. Not 1.
        // Try: 1100000: sum = 1*3+1*2 = 5. Need 1 mod 11. Diff = -4 mod 11 = 7. Use position 3 (weight 1): digit 7. CUI = 1170000+check
        // 1170000: sum = 1*3+1*2+7*1+0*7+0*5+0*3+0*2 = 3+2+7 = 12. 12%11=1. remainder=10 -> check=0
        var (isValid, normalizedCui, error) = CuiValidator.IsValidCui("11700000");

        Assert.True(isValid);
        Assert.Equal("11700000", normalizedCui);
        Assert.Null(error);
    }

    [Fact]
    public void IsValidCui_NormalizedCuiStripsRoPrefix()
    {
        var (_, normalizedCui, _) = CuiValidator.IsValidCui("RO16393852");

        Assert.Equal("16393852", normalizedCui);
        Assert.DoesNotContain("RO", normalizedCui);
    }

    [Fact]
    public void IsValidCui_MaxLength10Digits_ValidatesCorrectly()
    {
        // 10-digit CUI: all 9 weights are used
        // weights = [7,5,3,2,1,7,5,3,2], 9 data digits + 1 check digit
        // Try: 1000000000 -> sum = 1*7 = 7, remainder = 70%11 = 4, check=4 -> 1000000004
        var (isValid, _, error) = CuiValidator.IsValidCui("1000000004");

        Assert.True(isValid);
        Assert.Null(error);
    }
}
