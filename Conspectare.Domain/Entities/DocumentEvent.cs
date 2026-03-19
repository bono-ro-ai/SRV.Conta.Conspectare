namespace Conspectare.Domain.Entities;

public class DocumentEvent
{
    public virtual long Id { get; set; }
    public virtual long DocumentId { get; set; }
    public virtual long TenantId { get; set; }
    public virtual string EventType { get; set; }
    public virtual string FromStatus { get; set; }
    public virtual string ToStatus { get; set; }
    public virtual string Details { get; set; }
    public virtual DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; }
}
