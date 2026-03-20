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
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
}
