using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

public class ReviewServiceTests
{
    private readonly DocumentStatusWorkflow _workflow = new();

    private ReviewService CreateService(SharedConnectionSessionFactory sharedFactory)
    {
        var adapter = new SessionFactoryAdapter(sharedFactory);
        return new ReviewService(adapter, _workflow, NullLogger<ReviewService>.Instance);
    }

    private (Document document, ApiClient tenant) SeedReviewRequiredDocument(
        ISession session,
        ApiClient tenant = null,
        string status = null,
        bool withFlags = true)
    {
        if (tenant == null)
        {
            tenant = new ApiClient
            {
                Name = "Test Tenant",
                ApiKeyHash = "hash_test",
                ApiKeyPrefix = "dp_test_",
                IsActive = true,
                RateLimitPerMin = 100,
                MaxFileSizeMb = 50,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            using var tx0 = session.BeginTransaction();
            session.Save(tenant);
            tx0.Commit();
        }

        var utcNow = DateTime.UtcNow;
        var doc = new Document
        {
            TenantId = tenant.Id,
            Tenant = tenant,
            FileName = "review-test.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = 1024,
            InputFormat = InputFormat.Pdf,
            Status = status ?? DocumentStatus.ReviewRequired,
            RetryCount = 0,
            MaxRetries = 3,
            RawFileS3Key = "test/raw/review-test.pdf",
            DocumentType = "invoice",
            TriageConfidence = 0.75m,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        using var tx1 = session.BeginTransaction();
        session.Save(doc);
        tx1.Commit();

        if (withFlags)
        {
            var flag = new ReviewFlag
            {
                DocumentId = doc.Id,
                Document = doc,
                TenantId = tenant.Id,
                FlagType = "low_confidence",
                Severity = "warning",
                Message = "Confidence below threshold",
                IsResolved = false,
                CreatedAt = utcNow
            };
            using var tx2 = session.BeginTransaction();
            session.Save(flag);
            tx2.Commit();
            doc.ReviewFlags = new List<ReviewFlag> { flag };
        }

        return (doc, tenant);
    }

    private ApiClient CreateTenant(ISession session)
    {
        var tenant = new ApiClient
        {
            Name = "Test Tenant",
            ApiKeyHash = "hash_test",
            ApiKeyPrefix = "dp_test_",
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

    [Fact]
    public async Task ApproveAsync_ReviewRequiredDocument_TransitionsToCompleted()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var (doc, tenant) = SeedReviewRequiredDocument(session);

        var service = CreateService(sharedFactory);
        var result = await service.ApproveAsync(tenant.Id, doc.Id, "Looks good", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Completed, result.Data.Status);
        Assert.NotNull(result.Data.CompletedAt);
    }

    [Fact]
    public async Task ApproveAsync_ReviewRequiredDocument_ResolvesAllFlags()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var (doc, tenant) = SeedReviewRequiredDocument(session);

        var service = CreateService(sharedFactory);
        var result = await service.ApproveAsync(tenant.Id, doc.Id, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Data.ReviewFlags, f =>
        {
            Assert.True(f.IsResolved);
            Assert.NotNull(f.ResolvedAt);
        });
    }

    [Fact]
    public async Task ApproveAsync_ReviewRequiredDocument_CreatesAuditEvent()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var (doc, tenant) = SeedReviewRequiredDocument(session);

        var service = CreateService(sharedFactory);
        await service.ApproveAsync(tenant.Id, doc.Id, "Approved by admin", CancellationToken.None);

        using var verifySession = sharedFactory.OpenSession();
        var events = verifySession.QueryOver<DocumentEvent>()
            .Where(e => e.DocumentId == doc.Id)
            .And(e => e.EventType == "status_change")
            .List();

        Assert.Contains(events, e => e.ToStatus == DocumentStatus.Completed && e.Details == "Approved by admin");
    }

    [Fact]
    public async Task ApproveAsync_WrongStatus_ReturnsConflict()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var (doc, tenant) = SeedReviewRequiredDocument(session, status: DocumentStatus.Completed, withFlags: false);

        var service = CreateService(sharedFactory);
        var result = await service.ApproveAsync(tenant.Id, doc.Id, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task ApproveAsync_NonExistentDocument_ReturnsNotFound()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var tenant = CreateTenant(session);

        var service = CreateService(sharedFactory);
        var result = await service.ApproveAsync(tenant.Id, 999999, null, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact]
    public async Task RejectAsync_ReviewRequiredDocument_TransitionsToRejected()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var (doc, tenant) = SeedReviewRequiredDocument(session);

        var service = CreateService(sharedFactory);
        var result = await service.RejectAsync(tenant.Id, doc.Id, "Not a valid invoice", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DocumentStatus.Rejected, result.Data.Status);
    }

    [Fact]
    public async Task RejectAsync_ReviewRequiredDocument_CreatesAuditEventWithReason()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var (doc, tenant) = SeedReviewRequiredDocument(session);

        var service = CreateService(sharedFactory);
        await service.RejectAsync(tenant.Id, doc.Id, "Duplicate document", CancellationToken.None);

        using var verifySession = sharedFactory.OpenSession();
        var events = verifySession.QueryOver<DocumentEvent>()
            .Where(e => e.DocumentId == doc.Id)
            .And(e => e.EventType == "status_change")
            .List();

        Assert.Contains(events, e => e.ToStatus == DocumentStatus.Rejected && e.Details == "Duplicate document");
    }

    [Fact]
    public async Task RejectAsync_WrongStatus_ReturnsConflict()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var (doc, tenant) = SeedReviewRequiredDocument(session, status: DocumentStatus.Completed, withFlags: false);

        var service = CreateService(sharedFactory);
        var result = await service.RejectAsync(tenant.Id, doc.Id, "reason", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.StatusCode);
    }

    [Fact]
    public async Task RejectAsync_NonExistentDocument_ReturnsNotFound()
    {
        using var sharedFactory = new SharedConnectionSessionFactory();
        using var session = sharedFactory.OpenSession();
        var tenant = CreateTenant(session);

        var service = CreateService(sharedFactory);
        var result = await service.RejectAsync(tenant.Id, 999999, "reason", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(404, result.StatusCode);
    }
}
