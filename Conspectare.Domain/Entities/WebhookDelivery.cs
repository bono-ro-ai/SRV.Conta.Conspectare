namespace Conspectare.Domain.Entities;

public class WebhookDelivery
{
    public virtual long Id { get; set; }
    public virtual long DocumentId { get; set; }
    public virtual long TenantId { get; set; }
    public virtual string WebhookUrl { get; set; }
    public virtual string PayloadJson { get; set; }
    public virtual string Status { get; set; }
    public virtual int HttpStatusCode { get; set; }
    public virtual string ErrorMessage { get; set; }
    public virtual int AttemptCount { get; set; }
    public virtual int MaxAttempts { get; set; }
    public virtual DateTime? NextAttemptAt { get; set; }
    public virtual DateTime? LastAttemptAt { get; set; }
    public virtual DateTime? DeliveredAt { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
}
