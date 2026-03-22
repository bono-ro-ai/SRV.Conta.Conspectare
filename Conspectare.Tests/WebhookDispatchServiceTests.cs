using System.Net;
using Conspectare.Domain.Entities;
using Conspectare.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Conspectare.Tests;

public class WebhookDispatchServiceTests
{
    private static WebhookDelivery CreateDelivery(
        string webhookUrl = "https://example.com/webhook",
        int maxAttempts = 3) => new()
    {
        Id = 1,
        DocumentId = 100,
        TenantId = 1,
        WebhookUrl = webhookUrl,
        PayloadJson = "{\"event\":\"document.status_changed\",\"document_id\":100}",
        Status = "pending",
        AttemptCount = 0,
        MaxAttempts = maxAttempts,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static WebhookDispatchService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var logger = NullLogger<WebhookDispatchService>.Instance;
        return new WebhookDispatchService(httpClient, logger);
    }

    [Fact]
    public async Task DispatchAsync_2xxResponse_MarksDelivered()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);
        var delivery = CreateDelivery();

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Equal("delivered", delivery.Status);
        Assert.NotNull(delivery.DeliveredAt);
        Assert.Equal(200, delivery.HttpStatusCode);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task DispatchAsync_5xxResponse_SchedulesRetry()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        var service = CreateService(handler);
        var delivery = CreateDelivery();

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Equal("pending", delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.NotNull(delivery.NextAttemptAt);
        Assert.Equal(500, delivery.HttpStatusCode);
    }

    [Fact]
    public async Task DispatchAsync_4xxResponse_MarksFailedPermanently()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.BadRequest, "{}");
        var service = CreateService(handler);
        var delivery = CreateDelivery();

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Equal("failed_permanently", delivery.Status);
        Assert.Equal(400, delivery.HttpStatusCode);
        Assert.Contains("Client error", delivery.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_429Response_SchedulesRetry()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.TooManyRequests, "{}");
        var service = CreateService(handler);
        var delivery = CreateDelivery(maxAttempts: 3);
        delivery.AttemptCount = 0;

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Equal("pending", delivery.Status);
        Assert.NotNull(delivery.NextAttemptAt);
        Assert.Equal(429, delivery.HttpStatusCode);
    }

    [Fact]
    public async Task DispatchAsync_429AfterMaxAttempts_MarksDeliveryAsFailed()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.TooManyRequests, "{}");
        var service = CreateService(handler);
        var delivery = CreateDelivery(maxAttempts: 3);
        delivery.AttemptCount = 2;

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Equal("failed_permanently", delivery.Status);
        Assert.Equal(3, delivery.AttemptCount);
        Assert.Equal(429, delivery.HttpStatusCode);
        Assert.Contains("Rate-limited", delivery.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_MaxRetriesExceeded_MarksFailedPermanently()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        var service = CreateService(handler);
        var delivery = CreateDelivery(maxAttempts: 3);
        delivery.AttemptCount = 2;

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Equal("failed_permanently", delivery.Status);
        Assert.Equal(3, delivery.AttemptCount);
    }

    [Fact]
    public async Task DispatchAsync_NetworkError_SchedulesRetry()
    {
        var handler = new ThrowingHttpMessageHandler(new HttpRequestException("Connection refused"));
        var service = CreateService(handler);
        var delivery = CreateDelivery();

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Equal("pending", delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Equal(0, delivery.HttpStatusCode);
        Assert.Contains("Connection refused", delivery.ErrorMessage);
    }

    [Fact]
    public async Task DispatchAsync_ExponentialBackoff_IncreasesDelay()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        var service = CreateService(handler);

        var delivery1 = CreateDelivery();
        delivery1.AttemptCount = 0;
        await service.DispatchAsync(delivery1, CancellationToken.None);
        var firstRetryAt = delivery1.NextAttemptAt;

        var delivery2 = CreateDelivery();
        delivery2.AttemptCount = 1;
        await service.DispatchAsync(delivery2, CancellationToken.None);
        var secondRetryAt = delivery2.NextAttemptAt;

        Assert.NotNull(firstRetryAt);
        Assert.NotNull(secondRetryAt);
        Assert.True(secondRetryAt > firstRetryAt);
    }

    [Fact]
    public async Task DispatchAsync_PostsToCorrectUrl()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);
        var delivery = CreateDelivery("https://myapp.com/hooks/documents");

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Single(handler.RequestUrls);
        Assert.Equal("https://myapp.com/hooks/documents", handler.RequestUrls[0]);
    }

    [Fact]
    public async Task DispatchAsync_SendsPayloadAsJson()
    {
        var handler = new MockHttpMessageHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);
        var delivery = CreateDelivery();

        await service.DispatchAsync(delivery, CancellationToken.None);

        Assert.Single(handler.Requests);
        Assert.Contains("document.status_changed", handler.Requests[0]);
    }
}

internal class ThrowingHttpMessageHandler : HttpMessageHandler
{
    private readonly Exception _exception;

    public ThrowingHttpMessageHandler(Exception exception)
    {
        _exception = exception;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw _exception;
    }
}
