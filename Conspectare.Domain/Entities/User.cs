namespace Conspectare.Domain.Entities;

public class User
{
    public virtual long Id { get; set; }
    public virtual string Email { get; set; }
    public virtual string Name { get; set; }
    public virtual string PasswordHash { get; set; }
    public virtual string Role { get; set; } = "user";
    public virtual bool IsActive { get; set; } = true;
    public virtual int FailedLoginAttempts { get; set; }
    public virtual long? TenantId { get; set; }
    public virtual DateTime? LockedUntil { get; set; }
    public virtual DateTime? LastLoginAt { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
}
