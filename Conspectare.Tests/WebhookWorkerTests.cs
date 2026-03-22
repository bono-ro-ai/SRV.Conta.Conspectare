using Conspectare.Domain.Entities;
using Conspectare.Domain.Enums;
using Conspectare.Services;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Conspectare.Tests;

public class WebhookWorkerTests
{
    [Fact]
    public async Task DispatchAsync_SuccessfulDelivery_SetsDeliveredStatus()
    {
        var delivery = CreateDelivery();
        var handler = new TestHttpMessageHandler(System.Net.HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var logger = NullLogger<WebhookDispatchService>.Instance;
        var service = new WebhookDispatchService(httpClient, logger);

        await service.DispatchAsync(delivery, "test-secret", CancellationToken.None);

        Assert.Equal("delivered", delivery.Status);
        Assert.NotNull(delivery.DeliveredAt);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task DispatchAsync_ServerError_SchedulesRetryWhenAttemptsRemain()
    {
        var delivery = CreateDelivery(attemptCount: 0, maxAttempts: 3);
        var handler = new TestHttpMessageHandler(System.Net.HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler);
        var logger = NullLogger<WebhookDispatchService>.Instance;
        var service = new WebhookDispatchService(httpClient, logger);

        await service.DispatchAsync(delivery, "test-secret", CancellationToken.None);

        Assert.NotEqual("delivered", delivery.Status);
        Assert.NotEqual("failed_permanently", delivery.Status);
        Assert.NotNull(delivery.NextAttemptAt);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task DispatchAsync_ServerError_MaxAttemptsReached_FailsPermanently()
    {
        var delivery = CreateDelivery(attemptCount: 2, maxAttempts: 3);
        var handler = new TestHttpMessageHandler(System.Net.HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler);
        var logger = NullLogger<WebhookDispatchService>.Instance;
        var service = new WebhookDispatchService(httpClient, logger);

        await service.DispatchAsync(delivery, "test-secret", CancellationToken.None);

        Assert.Equal("failed_permanently", delivery.Status);
        Assert.Equal(3, delivery.AttemptCount);
    }

    [Fact]
    public async Task DispatchAsync_ClientError_FailsPermanently()
    {
        var delivery = CreateDelivery();
        var handler = new TestHttpMessageHandler(System.Net.HttpStatusCode.BadRequest);
        var httpClient = new HttpClient(handler);
        var logger = NullLogger<WebhookDispatchService>.Instance;
        var service = new WebhookDispatchService(httpClient, logger);

        await service.DispatchAsync(delivery, "test-secret", CancellationToken.None);

        Assert.Equal("failed_permanently", delivery.Status);
        Assert.Contains("Client error", delivery.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_RateLimited_SchedulesRetry()
    {
        var delivery = CreateDelivery();
        var handler = new TestHttpMessageHandler(System.Net.HttpStatusCode.TooManyRequests);
        var httpClient = new HttpClient(handler);
        var logger = NullLogger<WebhookDispatchService>.Instance;
        var service = new WebhookDispatchService(httpClient, logger);

        await service.DispatchAsync(delivery, "test-secret", CancellationToken.None);

        Assert.NotEqual("failed_permanently", delivery.Status);
        Assert.NotEqual("delivered", delivery.Status);
        Assert.NotNull(delivery.NextAttemptAt);
    }

    [Fact]
    public async Task DispatchAsync_HttpRequestException_HandlesGracefully()
    {
        var delivery = CreateDelivery();
        var handler = new TestHttpMessageHandler(throwException: true);
        var httpClient = new HttpClient(handler);
        var logger = NullLogger<WebhookDispatchService>.Instance;
        var service = new WebhookDispatchService(httpClient, logger);

        await service.DispatchAsync(delivery, "test-secret", CancellationToken.None);

        Assert.Equal(0, delivery.HttpStatusCode);
        Assert.NotNull(delivery.ErrorMessage);
    }

    [Fact]
    public void WebhookEnqueuer_MissingWebhookUrl_DoesNotEnqueue()
    {
        var doc = new Document
        {
            Id = 1,
            TenantId = 1,
            Status = DocumentStatus.Completed
        };
        var client = new ApiClient
        {
            Id = 1,
            WebhookUrl = null
        };

        var exception = Record.Exception(() => WebhookEnqueuer.EnqueueIfNeeded(doc, client, DateTime.UtcNow));

        Assert.Null(exception);
    }

    [Fact]
    public void WebhookEnqueuer_NonEligibleStatus_DoesNotEnqueue()
    {
        var doc = new Document
        {
            Id = 1,
            TenantId = 1,
            Status = DocumentStatus.PendingTriage
        };
        var client = new ApiClient
        {
            Id = 1,
            WebhookUrl = "https://example.com/webhook"
        };

        var exception = Record.Exception(() => WebhookEnqueuer.EnqueueIfNeeded(doc, client, DateTime.UtcNow));

        Assert.Null(exception);
    }

    private static WebhookDelivery CreateDelivery(int attemptCount = 0, int maxAttempts = 3)
    {
        return new WebhookDelivery
        {
            Id = 1,
            DocumentId = 100,
            TenantId = 1,
            WebhookUrl = "https://example.com/webhook",
            PayloadJson = "{\"test\":true}",
            Status = "pending",
            AttemptCount = attemptCount,
            MaxAttempts = maxAttempts,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}

internal class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly System.Net.HttpStatusCode _statusCode;
    private readonly bool _throwException;

    public TestHttpMessageHandler(System.Net.HttpStatusCode statusCode = System.Net.HttpStatusCode.OK)
    {
        _statusCode = statusCode;
    }

    public TestHttpMessageHandler(bool throwException)
    {
        _throwException = throwException;
        _statusCode = System.Net.HttpStatusCode.OK;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_throwException)
            throw new HttpRequestException("Connection refused");

        return Task.FromResult(new HttpResponseMessage(_statusCode));
    }
}
