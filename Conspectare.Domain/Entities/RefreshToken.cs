namespace Conspectare.Domain.Entities;

public class RefreshToken
{
    public virtual long Id { get; set; }
    public virtual long UserId { get; set; }
    public virtual string TokenHash { get; set; }
    public virtual DateTime ExpiresAt { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime? RevokedAt { get; set; }
    public virtual long? ReplacedByTokenId { get; set; }
    public virtual User User { get; set; }
}
