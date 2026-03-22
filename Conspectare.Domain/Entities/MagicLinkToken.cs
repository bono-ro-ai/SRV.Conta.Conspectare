namespace Conspectare.Domain.Entities;

public class MagicLinkToken
{
    public virtual long Id { get; set; }
    public virtual long? UserId { get; set; }
    public virtual User User { get; set; }
    public virtual string TokenHash { get; set; } = string.Empty;
    public virtual string Email { get; set; } = string.Empty;
    public virtual DateTime ExpiresAt { get; set; }
    public virtual DateTime? UsedAt { get; set; }
    public virtual DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public virtual string IpAddress { get; set; }
}
