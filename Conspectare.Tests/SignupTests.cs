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
public class SignupTests : IDisposable
{
    private readonly SignupTestNHibernateHelper _helper;
    private readonly AuthService _authService;

    private static readonly JwtSettings TestJwtSettings = new()
    {
        Secret = "test-secret-key-that-is-long-enough-for-hmac-sha256-validation",
        Issuer = "test-issuer",
        Audience = "test-audience",
        AccessTokenExpirationMinutes = 15,
        RefreshTokenExpirationDays = 7
    };

    public SignupTests()
    {
        _helper = new SignupTestNHibernateHelper();
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
    public async Task SignupAsync_ValidInput_CreatesTenantAndUser()
    {
        var result = await _authService.SignupAsync("Test Company", "12345678", "test@example.com", "Password1234");

        Assert.True(result.IsSuccess);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal("test@example.com", result.Data.Email);
        Assert.Equal("user", result.Data.Role);
        Assert.True(result.Data.TenantId > 0);
        Assert.True(result.Data.UserId > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.Token));
        Assert.False(string.IsNullOrWhiteSpace(result.Data.PlainApiKey));
        Assert.StartsWith("csp_", result.Data.PlainApiKey);
        Assert.Equal(result.Data.PlainApiKey[..8], result.Data.ApiKeyPrefix);
    }

    [Fact]
    public async Task SignupAsync_DuplicateEmail_ReturnsConflict()
    {
        await _authService.SignupAsync("Company A", "11111111", "dup@example.com", "Password1234");
        var result = await _authService.SignupAsync("Company B", "22222222", "dup@example.com", "Password5678");

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task SignupAsync_SetsTrialExpiresAt()
    {
        var before = DateTime.UtcNow.AddDays(29);
        var result = await _authService.SignupAsync("Trial Co", "33333333", "trial@example.com", "Password1234");
        var after = DateTime.UtcNow.AddDays(31);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data.TrialExpiresAt > before);
        Assert.True(result.Data.TrialExpiresAt < after);
    }

    [Fact]
    public async Task SignupAsync_ReturnsApiKey()
    {
        var result = await _authService.SignupAsync("Key Co", "44444444", "key@example.com", "Password1234");

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Data.PlainApiKey));
        Assert.StartsWith("csp_", result.Data.PlainApiKey);
        Assert.Equal(8, result.Data.ApiKeyPrefix.Length);
    }

    [Fact]
    public async Task SignupAsync_StripsRoPrefixFromCui()
    {
        var result = await _authService.SignupAsync("RO Company", "RO55555555", "ro@example.com", "Password1234");

        Assert.True(result.IsSuccess);

        using var session = NHibernateConspectare.OpenSession();
        var apiClient = session.QueryOver<ApiClient>()
            .Where(c => c.Id == result.Data.TenantId)
            .SingleOrDefault();

        Assert.Equal("55555555", apiClient.Cui);
    }

    [Fact]
    public async Task SignupAsync_UserHasTenantId()
    {
        var result = await _authService.SignupAsync("Tenant Co", "66666666", "tenant@example.com", "Password1234");

        Assert.True(result.IsSuccess);

        using var session = NHibernateConspectare.OpenSession();
        var user = session.QueryOver<User>()
            .Where(u => u.Id == result.Data.UserId)
            .SingleOrDefault();

        Assert.Equal(result.Data.TenantId, user.TenantId);
    }
}

public sealed class SignupTestNHibernateHelper : INHibernateHelper, IDisposable
{
    private readonly string _dbName;
    private SqliteConnection _keepAliveConnection;

    public SignupTestNHibernateHelper()
    {
        _dbName = $"SignupTest_{Guid.NewGuid():N}";
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
