using System.Net;
using System.Security.Cryptography;
using System.Text;
using Conspectare.Domain.Entities;
using Conspectare.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Conspectare.Services;

public class WebhookDispatchService : IWebhookDispatchService
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private const int BaseBackoffSeconds = 30;

    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookDispatchService> _logger;

    public WebhookDispatchService(
        HttpClient httpClient,
        ILogger<WebhookDispatchService> logger)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = RequestTimeout;
        _logger = logger;
    }

    public async Task DispatchAsync(WebhookDelivery delivery, CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        delivery.AttemptCount++;
        delivery.LastAttemptAt = utcNow;
        delivery.UpdatedAt = utcNow;

        try
        {
            using var content = new StringContent(delivery.PayloadJson, Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, delivery.WebhookUrl) { Content = content };
            if (!string.IsNullOrEmpty(delivery.WebhookSecret))
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(delivery.WebhookSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(delivery.PayloadJson));
                var signature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
                request.Headers.Add("X-Webhook-Signature", signature);
            }
            using var response = await _httpClient.SendAsync(request, ct);
            delivery.HttpStatusCode = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                delivery.Status = "delivered";
                delivery.DeliveredAt = utcNow;
                _logger.LogInformation(
                    "Webhook delivered for document {DocumentId} to {Url} (HTTP {StatusCode})",
                    delivery.DocumentId, delivery.WebhookUrl, delivery.HttpStatusCode);
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                ScheduleRetry(delivery, utcNow);
                _logger.LogWarning(
                    "Webhook rate-limited for document {DocumentId} (429), will retry (attempt {Attempt}/{Max})",
                    delivery.DocumentId, delivery.AttemptCount, delivery.MaxAttempts);
            }
            else if ((int)response.StatusCode >= 500)
            {
                HandleServerError(delivery, utcNow);
            }
            else
            {
                delivery.Status = "failed_permanently";
                delivery.ErrorMessage = $"Client error: HTTP {delivery.HttpStatusCode}";
                _logger.LogWarning(
                    "Webhook failed permanently for document {DocumentId} (HTTP {StatusCode})",
                    delivery.DocumentId, delivery.HttpStatusCode);
            }
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            delivery.HttpStatusCode = 0;
            delivery.ErrorMessage = "Request timed out";
            HandleServerError(delivery, utcNow);
        }
        catch (HttpRequestException ex)
        {
            delivery.HttpStatusCode = 0;
            delivery.ErrorMessage = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
            HandleServerError(delivery, utcNow);
        }
    }

    private void HandleServerError(WebhookDelivery delivery, DateTime utcNow)
    {
        if (delivery.AttemptCount >= delivery.MaxAttempts)
        {
            delivery.Status = "failed_permanently";
            delivery.ErrorMessage ??= $"Max attempts ({delivery.MaxAttempts}) exceeded";
            _logger.LogWarning(
                "Webhook failed permanently for document {DocumentId} after {Attempts} attempts",
                delivery.DocumentId, delivery.AttemptCount);
        }
        else
        {
            ScheduleRetry(delivery, utcNow);
            _logger.LogWarning(
                "Webhook failed for document {DocumentId}, will retry (attempt {Attempt}/{Max})",
                delivery.DocumentId, delivery.AttemptCount, delivery.MaxAttempts);
        }
    }

    private static void ScheduleRetry(WebhookDelivery delivery, DateTime utcNow)
    {
        var backoffSeconds = BaseBackoffSeconds * Math.Pow(4, delivery.AttemptCount - 1);
        delivery.NextAttemptAt = utcNow.AddSeconds(backoffSeconds);
    }
}
