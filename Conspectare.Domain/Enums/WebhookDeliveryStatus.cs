namespace Conspectare.Domain.Enums;

public static class WebhookDeliveryStatus
{
    public const string Pending = "pending";
    public const string Delivered = "delivered";
    public const string FailedPermanently = "failed_permanently";
}
