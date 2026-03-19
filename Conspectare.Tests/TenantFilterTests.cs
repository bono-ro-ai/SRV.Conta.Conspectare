using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Infrastructure.Extensions;
using Conspectare.Tests.Helpers;
using Xunit;
using ISession = NHibernate.ISession;

namespace Conspectare.Tests;

public class TenantFilterTests
{
    [Fact]
    public void EnableTenantFilter_OnlyReturnsTenantDocuments()
    {
        using var session = TestSessionFactory.OpenSession();

        var tenant1 = new ApiClient
        {
            Name = "Tenant 1",
            ApiKeyHash = "hash1",
            ApiKeyPrefix = "dp_t1___",
            IsActive = true,
            RateLimitPerMin = 100,
            MaxFileSizeMb = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var tenant2 = new ApiClient
        {
            Name = "Tenant 2",
            ApiKeyHash = "hash2",
            ApiKeyPrefix = "dp_t2___",
            IsActive = true,
            RateLimitPerMin = 100,
            MaxFileSizeMb = 50,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using (var tx = session.BeginTransaction())
        {
            session.Save(tenant1);
            session.Save(tenant2);
            tx.Commit();
        }

        var doc1 = new Document
        {
            TenantId = tenant1.Id,
            Tenant = tenant1,
            FileName = "tenant1.xml",
            ContentType = "application/xml",
            FileSizeBytes = 1024,
            InputFormat = InputFormat.XmlEfactura,
            Status = DocumentStatus.Ingested,
            RawFileS3Key = "s3://bucket/tenant1.xml",
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var doc2 = new Document
        {
            TenantId = tenant2.Id,
            Tenant = tenant2,
            FileName = "tenant2.xml",
            ContentType = "application/xml",
            FileSizeBytes = 2048,
            InputFormat = InputFormat.XmlEfactura,
            Status = DocumentStatus.Ingested,
            RawFileS3Key = "s3://bucket/tenant2.xml",
            RetryCount = 0,
            MaxRetries = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        using (var tx = session.BeginTransaction())
        {
            session.Save(doc1);
            session.Save(doc2);
            tx.Commit();
        }

        // Enable filter for tenant 1
        session.EnableTenantFilter(tenant1.Id);

        var documents = session.QueryOver<Document>().List();

        Assert.Single(documents);
        Assert.Equal("tenant1.xml", documents[0].FileName);
        Assert.Equal(tenant1.Id, documents[0].TenantId);
    }
}
