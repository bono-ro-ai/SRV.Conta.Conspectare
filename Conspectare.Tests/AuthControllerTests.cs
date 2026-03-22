using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Conspectare.Api.Controllers;
using Conspectare.Api.DTOs;
using Conspectare.Domain.Entities;
using Conspectare.Services;
using Conspectare.Services.Configuration;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Conspectare.Tests;

[Collection("NHibernateSequential")]
public class AuthControllerTests : IDisposable
{
    private readonly AuthTestNHibernateHelper _helper;
    private readonly AuthService _authService;
    private readonly MockTenantContext _tenantContext;
    private readonly AuthController _controller;

    private static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "test-secret-key-that-is-long-enough-for-hmac-sha256-validation",
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    public AuthControllerTests()
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
        _tenantContext = new MockTenantContext { TenantId = 1, IsAdmin = true, ApiKeyPrefix = "csp_test" };
        _controller = new AuthController(_authService, _tenantContext, Options.Create(TestJwtSettings));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    public void Dispose()
    {
        _helper.Dispose();
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithToken()
    {
        await _authService.RegisterAsync("login@test.com", "Login User", "Password123");

        var result = await _controller.Login(new LoginRequest("login@test.com", "Password123"));

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.True(response.ExpiresAt > DateTime.UtcNow);
        Assert.Equal("login@test.com", response.User.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await _authService.RegisterAsync("wrong@test.com", "Wrong User", "Password123");

        var result = await _controller.Login(new LoginRequest("wrong@test.com", "bad_password"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
    }

    [Fact]
    public async Task Login_NonExistentUser_Returns401()
    {
        var result = await _controller.Login(new LoginRequest("nobody@test.com", "Password123"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status401Unauthorized, objectResult.StatusCode);
    }

    [Fact]
    public async Task Register_FirstUser_Returns201()
    {
        var result = await _controller.Register(new RegisterRequest("first@test.com", "First User", "Password123"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        var response = Assert.IsType<MessageResponse>(objectResult.Value);
        Assert.Equal("Registration successful.", response.Message);
    }

    [Fact]
    public async Task Register_SubsequentUser_Returns201WithUserRole()
    {
        await _authService.RegisterAsync("admin@test.com", "Admin User", "Password123");

        var result = await _controller.Register(new RegisterRequest("second@test.com", "Second User", "Password123"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
    }

    [Fact]
    public async Task Register_NonAdmin_Returns403()
    {
        _tenantContext.IsAdmin = false;
        var result = await _controller.Register(new RegisterRequest("test@test.com", "Test", "StrongPass123"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        _tenantContext.IsAdmin = true;
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400()
    {
        var result = await _controller.Register(new RegisterRequest("weak@test.com", "Weak User", "short"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Contains("10 characters", problem.Detail);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns400()
    {
        await _authService.RegisterAsync("dup@test.com", "First", "Password123");

        var result = await _controller.Register(new RegisterRequest("dup@test.com", "Second", "Password123"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task Register_MissingFields_Returns400()
    {
        var result = await _controller.Register(new RegisterRequest(null, null, null));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public async Task Me_WithValidClaims_ReturnsUserInfo()
    {
        var registerResult = await _authService.RegisterAsync("me@test.com", "Me User", "Password123");
        var user = registerResult.Data;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        var result = await _controller.Me();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserInfoResponse>(okResult.Value);
        Assert.Equal(user.Id, response.Id);
        Assert.Equal("me@test.com", response.Email);
        Assert.Equal("Me User", response.Name);
        Assert.Equal("admin", response.Role);
    }

    [Fact]
    public async Task Me_WithoutClaims_Returns401()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await _controller.Me();

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Refresh_WithValidCookie_Returns200()
    {
        await _authService.RegisterAsync("refresh@test.com", "Refresh User", "Password123");
        var loginResult = await _authService.LoginAsync("refresh@test.com", "Password123");
        var rawRefreshToken = loginResult.Data.RawRefreshToken;

        _controller.ControllerContext.HttpContext.Request.Headers.Append("Cookie", $"refresh_token={rawRefreshToken}");

        var result = await _controller.Refresh();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.True(response.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401()
    {
        var result = await _controller.Refresh();

        var objectResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
    }

    [Fact]
    public async Task Login_NullRequest_Returns400()
    {
        var result = await _controller.Login(null);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public async Task Revoke_WithValidUser_Returns204()
    {
        var registerResult = await _authService.RegisterAsync("revoke@test.com", "Revoke User", "Password123");
        var user = registerResult.Data;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, "TestScheme");
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);

        var result = await _controller.Revoke();

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Revoke_WithoutClaims_Returns401()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await _controller.Revoke();

        Assert.IsType<UnauthorizedResult>(result);
    }
}
