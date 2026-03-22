using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.NHibernate.Helpers;
using Conspectare.Services;
using Conspectare.Services.Configuration;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Conspectare.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

[Collection("NHibernateSequential")]
public class AuthServiceTests : IDisposable
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

    public AuthServiceTests()
    {
        _helper = new AuthTestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(_helper);
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendMagicLinkEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
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
    public async Task Register_FirstUser_GetsAdminRole()
    {
        var result = await _authService.RegisterAsync("admin@test.com", "Admin User", "password123");

        Assert.True(result.IsSuccess);
        Assert.Equal("admin", result.Data.Role);
    }

    [Fact]
    public async Task Register_SubsequentUser_GetsUserRole()
    {
        await _authService.RegisterAsync("first@test.com", "First User", "password123");
        var result = await _authService.RegisterAsync("second@test.com", "Second User", "password123");

        Assert.True(result.IsSuccess);
        Assert.Equal("user", result.Data.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsBadRequest()
    {
        await _authService.RegisterAsync("dup@test.com", "User One", "password123");
        var result = await _authService.RegisterAsync("dup@test.com", "User Two", "password456");

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        await _authService.RegisterAsync("user@test.com", "Test User", "password123");
        var result = await _authService.LoginAsync("user@test.com", "password123");

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.RawRefreshToken));
        Assert.True(result.Data.ExpiresAt > DateTime.UtcNow);
        Assert.Equal("user@test.com", result.Data.User.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        await _authService.RegisterAsync("user@test.com", "Test User", "password123");
        var result = await _authService.LoginAsync("user@test.com", "wrong_password");

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Login_NonExistentUser_ReturnsUnauthorized()
    {
        var result = await _authService.LoginAsync("nobody@test.com", "password123");

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Login_Lockout_After5FailedAttempts()
    {
        await _authService.RegisterAsync("lockout@test.com", "Lock User", "password123");

        for (var i = 0; i < 5; i++)
        {
            await _authService.LoginAsync("lockout@test.com", "wrong");
        }

        var result = await _authService.LoginAsync("lockout@test.com", "password123");

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("locked", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_LockoutExpires_After15Minutes()
    {
        await _authService.RegisterAsync("expire@test.com", "Expire User", "password123");

        for (var i = 0; i < 5; i++)
        {
            await _authService.LoginAsync("expire@test.com", "wrong");
        }

        using var session = NHibernateConspectare.OpenSession();
        using (var tx = session.BeginTransaction())
        {
            var user = session.QueryOver<User>()
                .Where(u => u.Email == "expire@test.com")
                .SingleOrDefault();
            user.LockedUntil = DateTime.UtcNow.AddMinutes(-1);
            session.Update(user);
            tx.Commit();
        }

        var result = await _authService.LoginAsync("expire@test.com", "password123");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RefreshToken_ValidToken_ReturnsNewToken()
    {
        await _authService.RegisterAsync("refresh@test.com", "Refresh User", "password123");
        var loginResult = await _authService.LoginAsync("refresh@test.com", "password123");

        Assert.True(loginResult.IsSuccess);
        var rawRefreshToken = loginResult.Data.RawRefreshToken;

        var refreshResult = await _authService.RefreshTokenAsync(rawRefreshToken);

        Assert.True(refreshResult.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(refreshResult.Data.Token));
        Assert.NotEqual(loginResult.Data.Token, refreshResult.Data.Token);
    }

    [Fact]
    public async Task RefreshToken_RevokedToken_RevokesAllUserTokens()
    {
        await _authService.RegisterAsync("revoke@test.com", "Revoke User", "password123");
        var loginResult = await _authService.LoginAsync("revoke@test.com", "password123");
        var firstRefreshToken = loginResult.Data.RawRefreshToken;

        var refreshResult = await _authService.RefreshTokenAsync(firstRefreshToken);
        Assert.True(refreshResult.IsSuccess);

        var reuseResult = await _authService.RefreshTokenAsync(firstRefreshToken);
        Assert.False(reuseResult.IsSuccess);
        Assert.Equal(401, reuseResult.StatusCode);
        Assert.Contains("revoked", reuseResult.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshToken_ExpiredToken_ReturnsUnauthorized()
    {
        await _authService.RegisterAsync("expired@test.com", "Expired User", "password123");
        var loginResult = await _authService.LoginAsync("expired@test.com", "password123");

        using var session = NHibernateConspectare.OpenSession();
        using (var tx = session.BeginTransaction())
        {
            var tokens = session.QueryOver<RefreshToken>().List();
            foreach (var t in tokens)
            {
                t.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
                session.Update(t);
            }
            tx.Commit();
        }

        var result = await _authService.RefreshTokenAsync(loginResult.Data.RawRefreshToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(401, result.StatusCode);
    }
}

public sealed class AuthTestNHibernateHelper : INHibernateHelper, IDisposable
{
    private readonly string _dbName;
    private SqliteConnection _keepAliveConnection;

    public AuthTestNHibernateHelper()
    {
        _dbName = $"AuthTest_{Guid.NewGuid():N}";
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
