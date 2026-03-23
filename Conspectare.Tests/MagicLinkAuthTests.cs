using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.NHibernate.Helpers;
using Conspectare.Services;
using Conspectare.Services.Auth;
using Conspectare.Services.Configuration;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Conspectare.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

[Collection("NHibernateSequential")]
public class MagicLinkAuthTests : IDisposable
{
    private readonly MagicLinkTestNHibernateHelper _helper;
    private readonly AuthService _authService;
    private readonly Mock<IEmailService> _emailServiceMock;

    private static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "test-secret-key-that-is-long-enough-for-hmac-sha256-validation",
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    private static readonly AppSettings TestAppSettings = new()
    {
        FrontendUrl = "https://app.test.com"
    };

    public MagicLinkAuthTests()
    {
        _helper = new MagicLinkTestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(_helper);
        _emailServiceMock = new Mock<IEmailService>();
        _emailServiceMock.Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _authService = new AuthService(
            Options.Create(TestJwtSettings),
            _emailServiceMock.Object,
            Options.Create(TestAppSettings),
            NullLogger<AuthService>.Instance,
            Options.Create(new GoogleAuthSettings()),
            new Mock<IGoogleTokenValidator>().Object);
    }

    public void Dispose()
    {
        _helper.Dispose();
    }

    [Fact]
    public async Task SendMagicLink_NewUser_CreatesUserAndSendsEmail()
    {
        var result = await _authService.SendMagicLinkAsync("newuser@test.com", "127.0.0.1");

        Assert.True(result.IsSuccess);
        Assert.Contains("link de autentificare", result.Data);
        _emailServiceMock.Verify(e => e.SendMagicLinkEmailAsync("newuser@test.com", It.Is<string>(u => u.Contains("app.test.com"))), Times.Once);

        var user = new Services.Queries.LoadUserByEmailQuery("newuser@test.com").Execute();
        Assert.NotNull(user);
        Assert.Null(user.PasswordHash);
        Assert.Equal("user", user.Role);
    }

    [Fact]
    public async Task SendMagicLink_ExistingUser_SendsEmailWithoutCreatingNew()
    {
        await _authService.RegisterAsync("existing@test.com", "Existing User", "StrongPass123");

        var result = await _authService.SendMagicLinkAsync("existing@test.com", "127.0.0.1");

        Assert.True(result.IsSuccess);
        _emailServiceMock.Verify(e => e.SendMagicLinkEmailAsync("existing@test.com", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SendMagicLink_SecondUser_GetsUserRole()
    {
        await _authService.SendMagicLinkAsync("first@test.com", "127.0.0.1");
        await _authService.SendMagicLinkAsync("second@test.com", "127.0.0.1");

        var secondUser = new Services.Queries.LoadUserByEmailQuery("second@test.com").Execute();
        Assert.Equal("user", secondUser.Role);
    }

    [Fact]
    public async Task SendMagicLink_EmailUrlContainsToken()
    {
        string capturedUrl = null;
        _emailServiceMock.Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, url) => capturedUrl = url)
            .Returns(Task.CompletedTask);

        await _authService.SendMagicLinkAsync("url@test.com", "127.0.0.1");

        Assert.NotNull(capturedUrl);
        Assert.Contains("/auth/magic-link?token=", capturedUrl);
        Assert.StartsWith("https://app.test.com/", capturedUrl);
    }

    [Fact]
    public async Task VerifyMagicLink_ValidToken_ReturnsAuthResult()
    {
        string capturedUrl = null;
        _emailServiceMock.Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, url) => capturedUrl = url)
            .Returns(Task.CompletedTask);

        await _authService.SendMagicLinkAsync("verify@test.com", "127.0.0.1");

        var rawToken = ExtractTokenFromUrl(capturedUrl);
        var result = await _authService.VerifyMagicLinkAsync(rawToken);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RawRefreshToken));
        Assert.Equal("verify@test.com", result.Data.User.Email);
    }

    [Fact]
    public async Task VerifyMagicLink_InvalidToken_ReturnsBadRequest()
    {
        var result = await _authService.VerifyMagicLinkAsync("nonexistent-token");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task VerifyMagicLink_UsedToken_ReturnsBadRequest()
    {
        string capturedUrl = null;
        _emailServiceMock.Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, url) => capturedUrl = url)
            .Returns(Task.CompletedTask);

        await _authService.SendMagicLinkAsync("used@test.com", "127.0.0.1");
        var rawToken = ExtractTokenFromUrl(capturedUrl);

        await _authService.VerifyMagicLinkAsync(rawToken);
        var secondResult = await _authService.VerifyMagicLinkAsync(rawToken);

        Assert.False(secondResult.IsSuccess);
        Assert.Equal(400, secondResult.StatusCode);
    }

    [Fact]
    public async Task VerifyMagicLink_ExpiredToken_ReturnsBadRequest()
    {
        string capturedUrl = null;
        _emailServiceMock.Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Callback<string, string>((_, url) => capturedUrl = url)
            .Returns(Task.CompletedTask);

        await _authService.SendMagicLinkAsync("expired@test.com", "127.0.0.1");
        var rawToken = ExtractTokenFromUrl(capturedUrl);

        using var session = NHibernateConspectare.OpenSession();
        using (var tx = session.BeginTransaction())
        {
            var tokens = session.QueryOver<MagicLinkToken>().List();
            foreach (var t in tokens)
            {
                t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                session.Update(t);
            }
            tx.Commit();
        }

        var result = await _authService.VerifyMagicLinkAsync(rawToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Login_UserWithNullPasswordHash_ReturnsBadRequest()
    {
        await _authService.SendMagicLinkAsync("magiconly@test.com", "127.0.0.1");

        var result = await _authService.LoginAsync("magiconly@test.com", "anypassword");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    private static string ExtractTokenFromUrl(string url)
    {
        var uri = new Uri(url);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        return query["token"];
    }
}

public sealed class MagicLinkTestNHibernateHelper : INHibernateHelper, IDisposable
{
    private readonly string _dbName;
    private SqliteConnection _keepAliveConnection;

    public MagicLinkTestNHibernateHelper()
    {
        _dbName = $"MagicLinkTest_{Guid.NewGuid():N}";
        var connStr = $"Data Source={_dbName};Mode=Memory;Cache=Shared";
        _keepAliveConnection = new SqliteConnection(connStr);
        _keepAliveConnection.Open();

        using var session = SessionFactory.WithOptions().Connection(_keepAliveConnection).OpenSession();
        new SchemaExport(TestSessionFactory.Configuration).Execute(false, true, false, _keepAliveConnection, null);
    }

    public INHibernateHelper Configure<TMapping>(string connectionString,
        bool showSql = false, bool formatSql = false)
    {
        return this;
    }

    public ISessionFactory SessionFactory => TestSessionFactory.Instance;

    public ISession OpenSession()
    {
        var conn = new SqliteConnection($"Data Source={_dbName};Mode=Memory;Cache=Shared");
        conn.Open();
        return SessionFactory.WithOptions().Connection(conn).OpenSession();
    }

    public IStatelessSession OpenStatelessSession()
    {
        var conn = new SqliteConnection($"Data Source={_dbName};Mode=Memory;Cache=Shared");
        conn.Open();
        return SessionFactory.WithStatelessOptions().Connection(conn).OpenStatelessSession();
    }

    public void Dispose()
    {
        _keepAliveConnection?.Dispose();
    }
}
