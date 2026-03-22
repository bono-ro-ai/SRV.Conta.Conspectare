using System.Security.Cryptography;
using System.Text;
using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.NHibernate.Helpers;
using Conspectare.Services;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Interfaces;
using Conspectare.Tests.Helpers;
using Microsoft.Data.Sqlite;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

[Collection("NHibernateSequential")]
public class TenantSettingsTests : IDisposable
{
    private readonly TenantSettingsTestNHibernateHelper _helper;
    private readonly TenantSettingsService _service;
    private readonly TenantContext _tenantContext;
    private readonly long _tenantId;

    public TenantSettingsTests()
    {
        _helper = new TenantSettingsTestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(_helper);

        var now = DateTime.UtcNow;
        var apiClient = new ApiClient
        {
            Name = "Test Company",
            CompanyName = "Test Company",
            Cui = "12345678",
            ContactEmail = "test@example.com",
            ApiKeyHash = "abc123",
            ApiKeyPrefix = "csp_test",
            IsActive = true,
            IsAdmin = false,
            RateLimitPerMin = 60,
            MaxFileSizeMb = 10,
            WebhookUrl = "https://example.com/webhook",
            WebhookSecret = "secret123",
            TrialExpiresAt = now.AddDays(30),
            CreatedAt = now,
            UpdatedAt = now
        };

        using var session = NHibernateConspectare.OpenSession();
        using var tran = session.BeginTransaction();
        session.Save(apiClient);
        tran.Commit();
        _tenantId = apiClient.Id;

        _tenantContext = new TenantContext { TenantId = _tenantId };
        _service = new TenantSettingsService(_tenantContext);
    }

    public void Dispose()
    {
        _helper.Dispose();
    }

    [Fact]
    public async Task GetSettings_ReturnsCurrentTenantInfo()
    {
        var result = await _service.GetSettingsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(_tenantId, result.Data.TenantId);
        Assert.Equal("Test Company", result.Data.CompanyName);
        Assert.Equal("12345678", result.Data.Cui);
        Assert.Equal("test@example.com", result.Data.ContactEmail);
        Assert.Equal("https://example.com/webhook", result.Data.WebhookUrl);
        Assert.True(result.Data.HasWebhookSecret);
        Assert.True(result.Data.IsTrialActive);
    }

    [Fact]
    public async Task UpdateSettings_UpdatesCompanyName()
    {
        var input = new UpdateTenantSettingsInput("New Name", null, null, null);
        var result = await _service.UpdateSettingsAsync(input);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Data.CompanyName);

        var reloaded = await _service.GetSettingsAsync();
        Assert.Equal("New Name", reloaded.Data.CompanyName);
    }

    [Fact]
    public async Task RotateApiKey_ReturnsNewKey()
    {
        var result = await _service.RotateApiKeyAsync();

        Assert.True(result.IsSuccess);
        Assert.StartsWith("csp_", result.Data.PlainApiKey);
        Assert.Equal(8, result.Data.ApiKeyPrefix.Length);
    }

    [Fact]
    public async Task RotateApiKey_InvalidatesOldKey()
    {
        var oldSettings = await _service.GetSettingsAsync();
        var oldPrefix = oldSettings.Data.ApiKeyPrefix;

        var result = await _service.RotateApiKeyAsync();

        Assert.True(result.IsSuccess);
        Assert.NotEqual(oldPrefix, result.Data.ApiKeyPrefix);

        using var session = NHibernateConspectare.OpenSession();
        var apiClient = session.QueryOver<ApiClient>()
            .Where(c => c.Id == _tenantId)
            .SingleOrDefault();

        var newHash = SHA256.HashData(Encoding.UTF8.GetBytes(result.Data.PlainApiKey));
        var newHashHex = Convert.ToHexStringLower(newHash);
        Assert.Equal(newHashHex, apiClient.ApiKeyHash);
        Assert.Equal(result.Data.ApiKeyPrefix, apiClient.ApiKeyPrefix);
    }
}

public sealed class TenantSettingsTestNHibernateHelper : INHibernateHelper, IDisposable
{
    private readonly string _dbName;
    private SqliteConnection _keepAliveConnection;

    public TenantSettingsTestNHibernateHelper()
    {
        _dbName = $"TenantSettingsTest_{Guid.NewGuid():N}";
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
