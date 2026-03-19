using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Conspectare.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NHibernate.Cfg;
using NHibernate.Tool.hbm2ddl;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

public class MockStorageService : IStorageService
{
    private readonly Dictionary<string, byte[]> _store = new();
    public bool ShouldThrow { get; set; }

    public Task<string> UploadAsync(string key, Stream data, string contentType, CancellationToken ct = default)
    {
        if (ShouldThrow)
            throw new InvalidOperationException("Simulated S3 failure");

        using var ms = new MemoryStream();
        data.CopyTo(ms);
        _store[key] = ms.ToArray();
        return Task.FromResult(key);
    }

    public Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        if (_store.TryGetValue(key, out var data))
            return Task.FromResult<Stream>(new MemoryStream(data));
        throw new KeyNotFoundException($"Key '{key}' not found.");
    }

    public Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return Task.FromResult(_store.ContainsKey(key));
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<string> GeneratePresignedUrlAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        return Task.FromResult($"https://mock-s3/{key}?expires={expiry.TotalSeconds}");
    }
}

public class MockTenantContext : ITenantContext
{
    public long TenantId { get; set; }
    public string ApiKeyPrefix { get; set; }
    public int RateLimitPerMin { get; set; }
}

/// <summary>
/// Per-test SQLite connection + session factory wrapper.
/// All sessions share a single in-memory SQLite connection so data persists across
/// multiple OpenSession() calls within one test.
/// </summary>
public sealed class SharedConnectionSessionFactory : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly NHibernate.ISessionFactory _factory;
    private readonly Configuration _cfg;

    public SharedConnectionSessionFactory()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        // Reuse the already-built factory from TestSessionFactory (avoids re-mapping)
        _factory = TestSessionFactory.Instance;
        _cfg = TestSessionFactory.Configuration;

        // Create schema on the shared connection
        new SchemaExport(_cfg).Execute(false, true, false, _connection, null);
    }

    public ISession OpenSession()
    {
        return _factory.WithOptions().Connection(_connection).OpenSession();
    }

    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}

public class DocumentServiceTests
{
    private readonly MockStorageService _storageService = new();
    private readonly MockTenantContext _tenantContext = new() { TenantId = 1, ApiKeyPrefix = "dp_test_" };
    private readonly DocumentStatusWorkflow _workflow = new();
    private readonly ILogger<DocumentService> _logger = NullLogger<DocumentService>.Instance;

    private DocumentService CreateService(SharedConnectionSessionFactory sharedFactory)
    {
        // Create a thin adapter that delegates OpenSession to the shared factory
        var adapter = new SessionFactoryAdapter(sharedFactory);
        return new DocumentService(adapter, _storageService, _tenantContext, _workflow, _logger);
    }

    private ApiClient CreateTenant(ISession session, string name = "Test Tenant")
    {
        var tenant = new ApiClient
        {
            Name = name,
            ApiKeyHash = $"hash_{name}",
            ApiKeyPrefix = $"dp_{name}_",
            IsActive = true,
            RateLimitPerMin = 100,
            MaxFileSizeMb = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using var tx = session.BeginTransaction();
        session.Save(tenant);
        tx.Commit();
        return tenant;
    }

    private (Stream stream, string fileName, string contentType) CreateTestFile(string content = "test file content")
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return (stream, "invoice.xml", "text/xml");
    }

    [Fact]
    public async Task IngestAsync_ValidFile_CreatesDocumentWithPendingTriageStatus()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant = CreateTenant(setupSession);
        _tenantContext.TenantId = tenant.Id;

        var service = CreateService(sharedFactory);

        var (stream, fileName, contentType) = CreateTestFile();
        var result = await service.IngestAsync(stream, fileName, contentType, "ext-001", "client-ref", "{}", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(202, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(DocumentStatus.PendingTriage, result.Data.Status);
        Assert.Equal(fileName, result.Data.FileName);
        Assert.Equal(InputFormat.XmlEfactura, result.Data.InputFormat);
        Assert.Equal(tenant.Id, result.Data.TenantId);
        Assert.True(result.Data.Id > 0);
    }

    [Fact]
    public async Task IngestAsync_DuplicateExternalRef_ReturnsExistingDocument()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant = CreateTenant(setupSession);
        _tenantContext.TenantId = tenant.Id;

        var service = CreateService(sharedFactory);

        var (stream1, fileName1, contentType1) = CreateTestFile("file1");
        var result1 = await service.IngestAsync(stream1, fileName1, contentType1, "dup-ref", null, null, CancellationToken.None);
        Assert.True(result1.IsSuccess);

        var (stream2, fileName2, contentType2) = CreateTestFile("file2");
        var result2 = await service.IngestAsync(stream2, fileName2, contentType2, "dup-ref", null, null, CancellationToken.None);

        Assert.True(result2.IsSuccess);
        Assert.Equal(200, result2.StatusCode);
        Assert.Equal(result1.Data.Id, result2.Data.Id);
    }

    [Fact]
    public async Task IngestAsync_EmptyFileName_ReturnsBadRequest()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        var service = CreateService(sharedFactory);

        var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await service.IngestAsync(stream, "", "text/xml", null, null, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingDocument_ReturnsSuccess()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant = CreateTenant(setupSession);
        _tenantContext.TenantId = tenant.Id;

        var service = CreateService(sharedFactory);

        var (stream, fileName, contentType) = CreateTestFile();
        var ingestResult = await service.IngestAsync(stream, fileName, contentType, null, null, null, CancellationToken.None);
        Assert.True(ingestResult.IsSuccess);

        var getResult = await service.GetByIdAsync(ingestResult.Data.Id, CancellationToken.None);

        Assert.True(getResult.IsSuccess);
        Assert.Equal(ingestResult.Data.Id, getResult.Data.Id);
        Assert.Equal(fileName, getResult.Data.FileName);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNotFound()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant = CreateTenant(setupSession);
        _tenantContext.TenantId = tenant.Id;

        var service = CreateService(sharedFactory);

        var result = await service.GetByIdAsync(999999, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyCurrentTenantDocuments()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant1 = CreateTenant(setupSession, name: "Tenant A");
        var tenant2 = CreateTenant(setupSession, name: "Tenant B");

        // Ingest for tenant 1
        _tenantContext.TenantId = tenant1.Id;
        var service = CreateService(sharedFactory);
        var (s1, f1, c1) = CreateTestFile("t1-file");
        await service.IngestAsync(s1, f1, c1, null, null, null, CancellationToken.None);

        // Ingest for tenant 2
        _tenantContext.TenantId = tenant2.Id;
        var (s2, f2, c2) = CreateTestFile("t2-file");
        await service.IngestAsync(s2, f2, c2, null, null, null, CancellationToken.None);

        // List for tenant 1 only
        _tenantContext.TenantId = tenant1.Id;
        var listResult = await service.ListAsync(null, null, null, null, 1, 50, CancellationToken.None);

        Assert.True(listResult.IsSuccess);
        Assert.Equal(1, listResult.Data.TotalCount);
        Assert.All(listResult.Data.Items, d => Assert.Equal(tenant1.Id, d.TenantId));
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant = CreateTenant(setupSession);
        _tenantContext.TenantId = tenant.Id;

        var service = CreateService(sharedFactory);

        // Ingest two documents (both will be PendingTriage)
        var (s1, f1, c1) = CreateTestFile("file1");
        await service.IngestAsync(s1, f1, c1, null, null, null, CancellationToken.None);
        var (s2, f2, c2) = CreateTestFile("file2");
        await service.IngestAsync(s2, f2, c2, null, null, null, CancellationToken.None);

        // Filter by PendingTriage should return 2
        var result = await service.ListAsync(DocumentStatus.PendingTriage, null, null, null, 1, 50, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data.TotalCount);

        // Filter by Completed should return 0
        var noResult = await service.ListAsync(DocumentStatus.Completed, null, null, null, 1, 50, CancellationToken.None);
        Assert.True(noResult.IsSuccess);
        Assert.Equal(0, noResult.Data.TotalCount);
    }

    [Fact]
    public async Task RetryAsync_FailedDocument_ResetsStatus()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant = CreateTenant(setupSession);
        _tenantContext.TenantId = tenant.Id;

        // Create a document directly in ExtractionFailed status
        var utcNow = DateTime.UtcNow;
        var doc = new Document
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            FileName = "failed.xml",
            ContentType = "text/xml",
            FileSizeBytes = 100,
            InputFormat = InputFormat.XmlEfactura,
            Status = DocumentStatus.ExtractionFailed,
            RetryCount = 0,
            MaxRetries = 3,
            RawFileS3Key = "test/raw/failed.xml",
            ErrorMessage = "Previous error",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        using (var tx = setupSession.BeginTransaction())
        {
            setupSession.Save(doc);
            tx.Commit();
        }

        var service = CreateService(sharedFactory);

        var result = await service.RetryAsync(doc.Id, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.PendingTriage, result.Data.Status);
        Assert.Equal(1, result.Data.RetryCount);
        Assert.Null(result.Data.ErrorMessage);
    }

    [Fact]
    public async Task RetryAsync_CompletedDocument_ReturnsBadRequest()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant = CreateTenant(setupSession);
        _tenantContext.TenantId = tenant.Id;

        var utcNow = DateTime.UtcNow;
        var doc = new Document
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            FileName = "completed.xml",
            ContentType = "text/xml",
            FileSizeBytes = 100,
            InputFormat = InputFormat.XmlEfactura,
            Status = DocumentStatus.Completed,
            RetryCount = 0,
            MaxRetries = 3,
            RawFileS3Key = "test/raw/completed.xml",
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            CompletedAt = utcNow
        };

        using (var tx = setupSession.BeginTransaction())
        {
            setupSession.Save(doc);
            tx.Commit();
        }

        var service = CreateService(sharedFactory);

        var result = await service.RetryAsync(doc.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task RetryAsync_MaxRetriesExceeded_ReturnsBadRequest()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var setupSession = sharedFactory.OpenSession();
        var tenant = CreateTenant(setupSession);
        _tenantContext.TenantId = tenant.Id;

        var utcNow = DateTime.UtcNow;
        var doc = new Document
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            FileName = "maxretry.xml",
            ContentType = "text/xml",
            FileSizeBytes = 100,
            InputFormat = InputFormat.XmlEfactura,
            Status = DocumentStatus.ExtractionFailed,
            RetryCount = 3,
            MaxRetries = 3,
            RawFileS3Key = "test/raw/maxretry.xml",
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        using (var tx = setupSession.BeginTransaction())
        {
            setupSession.Save(doc);
            tx.Commit();
        }

        var service = CreateService(sharedFactory);

        var result = await service.RetryAsync(doc.Id, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(400, result.StatusCode);
        Assert.Contains("Maximum retry count", result.Error);
    }
}
