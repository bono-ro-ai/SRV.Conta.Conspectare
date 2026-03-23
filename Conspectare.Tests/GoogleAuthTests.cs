using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.Configuration;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Conspectare.Tests;

[Collection("NHibernateSequential")]
public class GoogleAuthTests : IDisposable
{
    private readonly AuthTestNHibernateHelper _helper;
    private readonly Mock<IGoogleTokenValidator> _mockGoogleValidator;
    private readonly AuthService _authService;

    private static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "test-secret-key-that-is-long-enough-for-hmac-sha256-validation",
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    private static readonly GoogleAuthSettings TestGoogleSettings = new()
    {
        ClientId = "test-google-client-id",
        AllowedDomain = "bono.ro"
    };

    public GoogleAuthTests()
    {
        _helper = new AuthTestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(_helper);
        _mockGoogleValidator = new Mock<IGoogleTokenValidator>();
        _authService = new AuthService(
            Options.Create(TestJwtSettings),
            Options.Create(TestGoogleSettings),
            _mockGoogleValidator.Object);
    }

    public void Dispose()
    {
        _helper.Dispose();
    }

    [Fact]
    public async Task GoogleLogin_ValidBonoEmail_CreatesAdminUser()
    {
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("valid-credential", TestGoogleSettings.ClientId))
            .ReturnsAsync(new GoogleTokenPayload("google-sub-123", "user@bono.ro", "Test User", "https://photo.url/pic.jpg", true, "bono.ro"));

        var result = await _authService.GoogleLoginAsync("valid-credential");

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RawRefreshToken));
        Assert.Equal("user@bono.ro", result.Data.User.Email);
        Assert.Equal("Test User", result.Data.User.Name);
        Assert.Equal("admin", result.Data.User.Role);
        Assert.Equal("google-sub-123", result.Data.User.GoogleId);
        Assert.Equal("https://photo.url/pic.jpg", result.Data.User.AvatarUrl);
    }

    [Fact]
    public async Task GoogleLogin_NonBonoEmail_ReturnsForbidden()
    {
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("non-bono-credential", TestGoogleSettings.ClientId))
            .ReturnsAsync(new GoogleTokenPayload("google-sub-456", "user@gmail.com", "External User", null, true, null));

        var result = await _authService.GoogleLoginAsync("non-bono-credential");

        Assert.False(result.IsSuccess);
        Assert.Equal(403, result.StatusCode);
        Assert.Contains("bono.ro", result.Error);
    }

    [Fact]
    public async Task GoogleLogin_ExistingEmailUser_LinksGoogleId()
    {
        await _authService.RegisterAsync("existing@bono.ro", "Existing User", "password123");

        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("link-credential", TestGoogleSettings.ClientId))
            .ReturnsAsync(new GoogleTokenPayload("google-sub-789", "existing@bono.ro", "Existing User", "https://photo.url/avatar.jpg", true, "bono.ro"));

        var result = await _authService.GoogleLoginAsync("link-credential");

        Assert.True(result.IsSuccess);
        Assert.Equal("existing@bono.ro", result.Data.User.Email);
        Assert.Equal("google-sub-789", result.Data.User.GoogleId);
        Assert.Equal("https://photo.url/avatar.jpg", result.Data.User.AvatarUrl);
    }

    [Fact]
    public async Task GoogleLogin_ExistingGoogleIdUser_ReturnsToken()
    {
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), TestGoogleSettings.ClientId))
            .ReturnsAsync(new GoogleTokenPayload("google-sub-repeat", "repeat@bono.ro", "Repeat User", "https://photo.url/pic2.jpg", true, "bono.ro"));

        var firstResult = await _authService.GoogleLoginAsync("first-credential");
        Assert.True(firstResult.IsSuccess);

        var secondResult = await _authService.GoogleLoginAsync("second-credential");
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstResult.Data.User.Id, secondResult.Data.User.Id);
        Assert.Equal("repeat@bono.ro", secondResult.Data.User.Email);
    }

    [Fact]
    public async Task PasswordLogin_GoogleOnlyUser_ReturnsBadRequest()
    {
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("google-only-credential", TestGoogleSettings.ClientId))
            .ReturnsAsync(new GoogleTokenPayload("google-sub-only", "googleonly@bono.ro", "Google Only", null, true, "bono.ro"));

        var googleResult = await _authService.GoogleLoginAsync("google-only-credential");
        Assert.True(googleResult.IsSuccess);

        var passwordResult = await _authService.LoginAsync("googleonly@bono.ro", "somepassword");

        Assert.False(passwordResult.IsSuccess);
        Assert.Equal(400, passwordResult.StatusCode);
        Assert.Contains("Google", passwordResult.Error);
    }

    [Fact]
    public async Task GoogleLogin_InvalidCredential_ReturnsUnauthorized()
    {
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("invalid-credential", TestGoogleSettings.ClientId))
            .ThrowsAsync(new Exception("Invalid token"));

        var result = await _authService.GoogleLoginAsync("invalid-credential");

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task GoogleLogin_UnverifiedEmail_ReturnsUnauthorized()
    {
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("unverified-credential", TestGoogleSettings.ClientId))
            .ReturnsAsync(new GoogleTokenPayload("google-sub-unverified", "unverified@bono.ro", "Unverified", null, false, "bono.ro"));

        var result = await _authService.GoogleLoginAsync("unverified-credential");

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }
}
