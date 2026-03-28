using System.Net;
using System.Text;
using System.Text.Json;
using Conspectare.Api.Controllers;
using Conspectare.Api.DTOs;
using Conspectare.Services;
using Conspectare.Services.Auth;
using Conspectare.Services.Configuration;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Conspectare.Tests;

[Collection("NHibernateSequential")]
public class GoogleOAuthRedirectTests : IDisposable
{
    private readonly AuthTestNHibernateHelper _helper;
    private readonly AuthService _authService;
    private readonly Mock<IGoogleTokenValidator> _mockGoogleValidator;
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory;

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
        ClientSecret = "test-google-client-secret",
        AllowedDomain = "bono.ro"
    };

    private static readonly AppSettings TestAppSettings = new()
    {
        FrontendUrl = "https://test.com"
    };

    public GoogleOAuthRedirectTests()
    {
        _helper = new AuthTestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(_helper);
        _mockGoogleValidator = new Mock<IGoogleTokenValidator>();
        _mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _authService = new AuthService(
            Options.Create(TestJwtSettings),
            emailServiceMock.Object,
            Options.Create(TestAppSettings),
            NullLogger<AuthService>.Instance,
            Options.Create(TestGoogleSettings),
            _mockGoogleValidator.Object,
            new NoOpGoogleGroupChecker());
    }

    public void Dispose()
    {
        _helper.Dispose();
    }

    private AuthController CreateController()
    {
        var controller = new AuthController(
            _authService,
            new MockTenantContext { TenantId = 1, IsAdmin = true, ApiKeyPrefix = "csp_test" },
            Options.Create(TestJwtSettings),
            Options.Create(TestGoogleSettings),
            Options.Create(TestAppSettings),
            _mockHttpClientFactory.Object,
            NullLogger<AuthController>.Instance);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    [Fact]
    public void GoogleRedirect_ReturnsRedirectToGoogle()
    {
        var controller = CreateController();

        var result = controller.GoogleRedirect();

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains("accounts.google.com/o/oauth2/v2/auth", redirectResult.Url);
        Assert.Contains("client_id=test-google-client-id", redirectResult.Url);
        Assert.Contains("redirect_uri=", redirectResult.Url);
        Assert.Contains("response_type=code", redirectResult.Url);
        Assert.Contains("scope=openid", redirectResult.Url);
        Assert.Contains("state=", redirectResult.Url);
    }

    [Fact]
    public void GoogleRedirect_StateContainsThreeParts()
    {
        var controller = CreateController();

        var result = controller.GoogleRedirect();

        var redirectResult = Assert.IsType<RedirectResult>(result);
        var uri = new Uri(redirectResult.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var state = query["state"];
        Assert.NotNull(state);
        var parts = state.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Fact]
    public void GoogleRedirect_RedirectUriPointsToFrontend()
    {
        var controller = CreateController();

        var result = controller.GoogleRedirect();

        var redirectResult = Assert.IsType<RedirectResult>(result);
        Assert.Contains(Uri.EscapeDataString("https://test.com/auth/google/callback"), redirectResult.Url);
    }

    [Fact]
    public async Task GoogleCallback_MissingCode_Returns400()
    {
        var controller = CreateController();

        var result = await controller.GoogleCallback(
            new GoogleCallbackRequest("", "some-state"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("required", problem.Detail);
    }

    [Fact]
    public async Task GoogleCallback_MissingState_Returns400()
    {
        var controller = CreateController();

        var result = await controller.GoogleCallback(
            new GoogleCallbackRequest("some-code", ""), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("required", problem.Detail);
    }

    [Fact]
    public async Task GoogleCallback_InvalidState_Returns400()
    {
        var controller = CreateController();

        var result = await controller.GoogleCallback(
            new GoogleCallbackRequest("some-code", "invalid.state.value"), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("Invalid OAuth state", problem.Detail);
    }

    [Fact]
    public async Task GoogleCallback_TamperedStateSignature_Returns400()
    {
        var controller = CreateController();

        // Generate valid state, then tamper with the signature
        var redirectResult = Assert.IsType<RedirectResult>(controller.GoogleRedirect());
        var uri = new Uri(redirectResult.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var validState = query["state"]!;
        var parts = validState.Split('.');
        var tamperedState = $"{parts[0]}.{parts[1]}.TAMPERED_SIGNATURE";

        var result = await controller.GoogleCallback(
            new GoogleCallbackRequest("some-code", tamperedState), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("Invalid OAuth state", problem.Detail);
    }

    [Fact]
    public async Task GoogleCallback_ValidStateAndCode_ExchangesAndReturnsAuth()
    {
        var controller = CreateController();

        // Generate valid state via redirect
        var redirectResult = Assert.IsType<RedirectResult>(controller.GoogleRedirect());
        var uri = new Uri(redirectResult.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var validState = query["state"]!;

        // Mock Google token exchange
        var tokenJson = JsonSerializer.Serialize(new { id_token = "mock-id-token", access_token = "mock-access" });
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
            });
        var httpClient = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        // Mock Google token validator
        _mockGoogleValidator
            .Setup(v => v.ValidateAsync("mock-id-token", TestGoogleSettings.ClientId))
            .ReturnsAsync(new GoogleTokenPayload("google-sub-999", "user@bono.ro", "OAuth User", "https://pic.url", true, "bono.ro"));

        var result = await controller.GoogleCallback(
            new GoogleCallbackRequest("valid-auth-code", validState), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponse>(okResult.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        Assert.Equal("user@bono.ro", response.User.Email);
    }

    [Fact]
    public async Task GoogleCallback_GoogleTokenExchangeFails_Returns400()
    {
        var controller = CreateController();

        var redirectResult = Assert.IsType<RedirectResult>(controller.GoogleRedirect());
        var uri = new Uri(redirectResult.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var validState = query["state"]!;

        // Mock Google returning an error
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json")
            });
        var httpClient = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.GoogleCallback(
            new GoogleCallbackRequest("expired-code", validState), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("Failed to exchange", problem.Detail);
    }

    [Fact]
    public async Task GoogleCallback_GoogleReturnsNoIdToken_Returns400()
    {
        var controller = CreateController();

        var redirectResult = Assert.IsType<RedirectResult>(controller.GoogleRedirect());
        var uri = new Uri(redirectResult.Url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var validState = query["state"]!;

        // Mock Google returning tokens without id_token
        var tokenJson = JsonSerializer.Serialize(new { access_token = "mock-access" });
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(tokenJson, Encoding.UTF8, "application/json")
            });
        var httpClient = new HttpClient(mockHandler.Object);
        _mockHttpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var result = await controller.GoogleCallback(
            new GoogleCallbackRequest("valid-code", validState), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Contains("did not return an ID token", problem.Detail);
    }
}
