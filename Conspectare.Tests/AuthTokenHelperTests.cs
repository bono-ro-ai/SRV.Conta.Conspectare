using Conspectare.Services.Auth;
using Xunit;

namespace Conspectare.Tests;

public class AuthTokenHelperTests
{
    [Fact]
    public void GenerateRawToken_Returns64CharHexString()
    {
        var token = AuthTokenHelper.GenerateRawToken();

        Assert.Equal(64, token.Length);
        Assert.Matches("^[0-9a-f]+$", token);
    }

    [Fact]
    public void GenerateRawToken_ProducesUniqueTokens()
    {
        var token1 = AuthTokenHelper.GenerateRawToken();
        var token2 = AuthTokenHelper.GenerateRawToken();

        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void HashToken_ReturnsDeterministicSha256Hex()
    {
        var hash1 = AuthTokenHelper.HashToken("test-token");
        var hash2 = AuthTokenHelper.HashToken("test-token");

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length);
        Assert.Matches("^[0-9a-f]+$", hash1);
    }

    [Fact]
    public void HashToken_DifferentInputs_ProduceDifferentHashes()
    {
        var hash1 = AuthTokenHelper.HashToken("token-a");
        var hash2 = AuthTokenHelper.HashToken("token-b");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void MaskEmail_ValidEmail_MasksLocalPart()
    {
        var masked = AuthTokenHelper.MaskEmail("john.doe@example.com");

        Assert.Equal("joh***@example.com", masked);
    }

    [Fact]
    public void MaskEmail_ShortLocalPart_MasksAvailableChars()
    {
        var masked = AuthTokenHelper.MaskEmail("ab@example.com");

        Assert.Equal("ab***@example.com", masked);
    }

    [Fact]
    public void MaskEmail_NullOrEmpty_ReturnsStars()
    {
        Assert.Equal("***", AuthTokenHelper.MaskEmail(null));
        Assert.Equal("***", AuthTokenHelper.MaskEmail(""));
    }

    [Fact]
    public void MaskEmail_NoAtSign_ReturnsStars()
    {
        Assert.Equal("***", AuthTokenHelper.MaskEmail("invalid-email"));
    }
}
