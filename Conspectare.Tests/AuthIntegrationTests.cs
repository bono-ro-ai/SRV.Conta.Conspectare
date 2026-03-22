using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.Configuration;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Conspectare.Tests;

[Collection("NHibernateSequential")]
public class AuthIntegrationTests : IDisposable
{
    private readonly AuthTestNHibernateHelper _helper;
    private readonly AuthService _authService;

    private static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "test-secret-key-that-is-long-enough-for-hmac-sha256-validation",
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    public AuthIntegrationTests()
    {
        _helper = new AuthTestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(_helper);
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _authService = new AuthService(
            Options.Create(TestJwtSettings),
            emailServiceMock.Object,
            Options.Create(new AppSettings { FrontendUrl = "https://test.com" }),
            NullLogger<AuthService>.Instance);
    }

    public void Dispose()
    {
        _helper.Dispose();
    }

    [Fact]
    public async Task FullAuthFlow_RegisterLoginRefreshRevoke_WorksEndToEnd()
    {
        var registerResult = await _authService.RegisterAsync("e2e@test.com", "E2E User", "password123");
        Assert.True(registerResult.IsSuccess);
        Assert.Equal("admin", registerResult.Data.Role);

        var loginResult = await _authService.LoginAsync("e2e@test.com", "password123");
        Assert.True(loginResult.IsSuccess);
        var jwt = loginResult.Data.Token;
        var refreshToken = loginResult.Data.RawRefreshToken;
        Assert.False(string.IsNullOrWhiteSpace(jwt));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));

        var handler = new JwtSecurityTokenHandler();
        var parsedToken = handler.ReadJwtToken(jwt);
        Assert.Equal(registerResult.Data.Id.ToString(), parsedToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("e2e@test.com", parsedToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("E2E User", parsedToken.Claims.First(c => c.Type == "name").Value);
        Assert.Contains(parsedToken.Claims, c => c.Type == ClaimTypes.Role && c.Value == "admin");

        var refreshResult = await _authService.RefreshTokenAsync(refreshToken);
        Assert.True(refreshResult.IsSuccess);
        var newJwt = refreshResult.Data.Token;
        var newRefreshToken = refreshResult.Data.RawRefreshToken;
        Assert.NotEqual(jwt, newJwt);
        Assert.NotEqual(refreshToken, newRefreshToken);

        var reuseOldResult = await _authService.RefreshTokenAsync(refreshToken);
        Assert.False(reuseOldResult.IsSuccess);
        Assert.Equal(401, reuseOldResult.StatusCode);

        var userId = registerResult.Data.Id;
        var revokeResult = await _authService.RevokeAllAsync(userId);
        Assert.True(revokeResult.IsSuccess);

        var postRevokeRefresh = await _authService.RefreshTokenAsync(newRefreshToken);
        Assert.False(postRevokeRefresh.IsSuccess);
        Assert.Equal(401, postRevokeRefresh.StatusCode);
    }

    [Fact]
    public async Task FullAuthFlow_SecondUserGetsUserRole()
    {
        await _authService.RegisterAsync("admin@test.com", "Admin", "password123");

        var secondResult = await _authService.RegisterAsync("user@test.com", "Regular", "password123");
        Assert.True(secondResult.IsSuccess);
        Assert.Equal("user", secondResult.Data.Role);

        var loginResult = await _authService.LoginAsync("user@test.com", "password123");
        Assert.True(loginResult.IsSuccess);

        var handler = new JwtSecurityTokenHandler();
        var parsedToken = handler.ReadJwtToken(loginResult.Data.Token);
        Assert.Contains(parsedToken.Claims, c => c.Type == ClaimTypes.Role && c.Value == "user");
    }

    [Fact]
    public async Task FullAuthFlow_WrongPasswordThenCorrect_WorksAfterCorrection()
    {
        await _authService.RegisterAsync("retry@test.com", "Retry User", "password123");

        var failResult = await _authService.LoginAsync("retry@test.com", "wrong");
        Assert.False(failResult.IsSuccess);

        var successResult = await _authService.LoginAsync("retry@test.com", "password123");
        Assert.True(successResult.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(successResult.Data.Token));
    }
}
