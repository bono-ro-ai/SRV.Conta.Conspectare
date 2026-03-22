using Conspectare.Services;
using Xunit;

namespace Conspectare.Tests;

public class DocumentRefAllocatorTests
{
    [Theory]
    [InlineData("16393852", "16393852")]
    [InlineData("RO16393852", "16393852")]
    [InlineData("ro16393852", "16393852")]
    [InlineData("Ro16393852", "16393852")]
    [InlineData("  RO16393852  ", "16393852")]
    [InlineData("  16393852  ", "16393852")]
    public void NormalizeFiscalCode_ValidInput_StripsRoPrefixAndTrims(string input, string expected)
    {
        var result = DocumentRefAllocator.NormalizeFiscalCode(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("RO")]
    [InlineData("ro")]
    [InlineData("  RO  ")]
    public void NormalizeFiscalCode_EmptyOrJustRo_ReturnsFallback007(string input)
    {
        var result = DocumentRefAllocator.NormalizeFiscalCode(input);
        Assert.Equal("007", result);
    }
}
