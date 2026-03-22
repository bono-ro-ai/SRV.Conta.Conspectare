using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using Conspectare.Services.Queries;
using Conspectare.Tests.Helpers;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

[Collection("NHibernateSequential")]
public class AggregateUsageForTenantQueryTests : IDisposable
{
    private readonly TestNHibernateHelper _db;

    public AggregateUsageForTenantQueryTests()
    {
        _db = new TestNHibernateHelper();
        NHibernateConspectare.ConfigureForTests(_db);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Execute_NoData_ReturnsZeros()
    {
        var result = new AggregateUsageForTenantQuery(999, DateTime.UtcNow.Date).Execute();

        Assert.Equal(0, result.DocumentsIngested);
        Assert.Equal(0, result.DocumentsProcessed);
        Assert.Equal(0, result.LlmInputTokens);
        Assert.Equal(0, result.LlmOutputTokens);
        Assert.Equal(0, result.LlmRequests);
        Assert.Equal(0, result.StorageBytes);
        Assert.Equal(0, result.ApiCalls);
    }

    [Fact]
    public void Execute_WithData_AggregatesCorrectly()
    {
        var targetDate = new DateTime(2026, 3, 21);

        using (var session = _db.OpenSession())
        using (var tx = session.BeginTransaction())
        {
            var tenant = new ApiClient
            {
                Name = "Test", ApiKeyHash = "h", ApiKeyPrefix = "csp_t", IsActive = true,
                RateLimitPerMin = 60, MaxFileSizeMb = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            session.Save(tenant);

            var doc1 = new Document
            {
                TenantId = tenant.Id, Tenant = tenant, ExternalRef = "r1", FileName = "a.pdf",
                ContentType = "application/pdf", FileSizeBytes = 1000, Status = "completed",
                CreatedAt = targetDate.AddHours(2), UpdatedAt = targetDate.AddHours(2),
                CompletedAt = targetDate.AddHours(3), ContentHash = "h1", InputFormat = "pdf",
                RawFileS3Key = "k1", DocumentRef = "REF-1", RetryCount = 0
            };
            var doc2 = new Document
            {
                TenantId = tenant.Id, Tenant = tenant, ExternalRef = "r2", FileName = "b.pdf",
                ContentType = "application/pdf", FileSizeBytes = 2000, Status = "pending",
                CreatedAt = targetDate.AddHours(5), UpdatedAt = targetDate.AddHours(5),
                ContentHash = "h2", InputFormat = "pdf", RawFileS3Key = "k2",
                DocumentRef = "REF-2", RetryCount = 0
            };
            session.Save(doc1);
            session.Save(doc2);

            session.Save(new ExtractionAttempt
            {
                DocumentId = doc1.Id, Document = doc1, TenantId = tenant.Id, AttemptNumber = 1,
                Phase = "extraction", ModelId = "claude", PromptVersion = "v1", Status = "completed",
                InputTokens = 500, OutputTokens = 200, CreatedAt = targetDate.AddHours(2)
            });
            session.Save(new ExtractionAttempt
            {
                DocumentId = doc1.Id, Document = doc1, TenantId = tenant.Id, AttemptNumber = 2,
                Phase = "extraction", ModelId = "claude", PromptVersion = "v1", Status = "completed",
                InputTokens = 300, OutputTokens = 150, CreatedAt = targetDate.AddHours(3)
            });

            session.Save(new DocumentArtifact
            {
                DocumentId = doc1.Id, Document = doc1, TenantId = tenant.Id, ArtifactType = "raw",
                S3Key = "s3/1", ContentType = "application/pdf", SizeBytes = 1024,
                RetentionDays = 30, CreatedAt = targetDate.AddHours(2)
            });
            session.Save(new DocumentArtifact
            {
                DocumentId = doc1.Id, Document = doc1, TenantId = tenant.Id, ArtifactType = "response",
                S3Key = "s3/2", ContentType = "application/json", SizeBytes = 512,
                RetentionDays = 30, CreatedAt = targetDate.AddHours(3)
            });

            session.Flush();
            tx.Commit();

            var result = new AggregateUsageForTenantQuery(tenant.Id, targetDate)
                .UseExternalSession(session).Execute();

            Assert.Equal(2, result.DocumentsIngested);
            Assert.Equal(1, result.DocumentsProcessed);
            Assert.Equal(800, result.LlmInputTokens);
            Assert.Equal(350, result.LlmOutputTokens);
            Assert.Equal(2, result.LlmRequests);
            Assert.Equal(1536, result.StorageBytes);
            Assert.Equal(2, result.ApiCalls);
        }
    }

    [Fact]
    public void Execute_NullTokens_TreatedAsZero()
    {
        var targetDate = new DateTime(2026, 3, 21);

        using var session = _db.OpenSession();
        using var tx = session.BeginTransaction();

        var tenant = new ApiClient
        {
            Name = "Test2", ApiKeyHash = "h2", ApiKeyPrefix = "csp_t2", IsActive = true,
            RateLimitPerMin = 60, MaxFileSizeMb = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        session.Save(tenant);

        var doc = new Document
        {
            TenantId = tenant.Id, Tenant = tenant, ExternalRef = "r3", FileName = "c.pdf",
            ContentType = "application/pdf", FileSizeBytes = 500, Status = "failed",
            CreatedAt = targetDate.AddHours(1), UpdatedAt = targetDate.AddHours(1),
            ContentHash = "h3", InputFormat = "pdf", RawFileS3Key = "k3",
            DocumentRef = "REF-3", RetryCount = 0
        };
        session.Save(doc);

        session.Save(new ExtractionAttempt
        {
            DocumentId = doc.Id, Document = doc, TenantId = tenant.Id, AttemptNumber = 1,
            Phase = "extraction", ModelId = "claude", PromptVersion = "v1", Status = "failed",
            InputTokens = null, OutputTokens = null, CreatedAt = targetDate.AddHours(1)
        });

        session.Flush();
        tx.Commit();

        var result = new AggregateUsageForTenantQuery(tenant.Id, targetDate)
            .UseExternalSession(session).Execute();

        Assert.Equal(1, result.DocumentsIngested);
        Assert.Equal(0, result.LlmInputTokens);
        Assert.Equal(0, result.LlmOutputTokens);
        Assert.Equal(1, result.LlmRequests);
    }

    [Fact]
    public void Execute_DifferentDay_NotIncluded()
    {
        var targetDate = new DateTime(2026, 3, 21);

        using var session = _db.OpenSession();
        using var tx = session.BeginTransaction();

        var tenant = new ApiClient
        {
            Name = "Test3", ApiKeyHash = "h3", ApiKeyPrefix = "csp_t3", IsActive = true,
            RateLimitPerMin = 60, MaxFileSizeMb = 10, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        session.Save(tenant);

        session.Save(new Document
        {
            TenantId = tenant.Id, Tenant = tenant, ExternalRef = "r4", FileName = "d.pdf",
            ContentType = "application/pdf", FileSizeBytes = 500, Status = "completed",
            CreatedAt = targetDate.AddDays(-1).AddHours(23), UpdatedAt = targetDate.AddDays(-1),
            ContentHash = "h4", InputFormat = "pdf", RawFileS3Key = "k4",
            DocumentRef = "REF-4", RetryCount = 0
        });

        session.Flush();
        tx.Commit();

        var result = new AggregateUsageForTenantQuery(tenant.Id, targetDate)
            .UseExternalSession(session).Execute();

        Assert.Equal(0, result.DocumentsIngested);
    }
}
