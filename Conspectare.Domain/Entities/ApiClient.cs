namespace Conspectare.Domain.Entities;

public class ApiClient
{
    public virtual long Id { get; set; }
    public virtual string Name { get; set; }
    public virtual string ApiKeyHash { get; set; }
    public virtual string ApiKeyPrefix { get; set; }
    public virtual bool IsActive { get; set; }
    public virtual bool IsAdmin { get; set; }
    public virtual int RateLimitPerMin { get; set; }
    public virtual int MaxFileSizeMb { get; set; }
    public virtual string WebhookUrl { get; set; }
    public virtual string WebhookSecret { get; set; }
    public virtual string CompanyName { get; set; }
    public virtual string Cui { get; set; }
    public virtual string ContactEmail { get; set; }
    public virtual DateTime? TrialExpiresAt { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
}
