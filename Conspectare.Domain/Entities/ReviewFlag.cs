namespace Conspectare.Domain.Entities;

public class ReviewFlag
{
    public virtual long Id { get; set; }
    public virtual long DocumentId { get; set; }
    public virtual long TenantId { get; set; }
    public virtual string FlagType { get; set; }
    public virtual string Severity { get; set; }
    public virtual string Message { get; set; }
    public virtual bool IsResolved { get; set; }
    public virtual DateTime? ResolvedAt { get; set; }
    public virtual DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; }
}
