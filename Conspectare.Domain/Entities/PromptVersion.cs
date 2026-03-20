namespace Conspectare.Domain.Entities;

public class PromptVersion
{
    public virtual long Id { get; set; }
    public virtual string Phase { get; set; }
    public virtual string DocumentType { get; set; }
    public virtual string Version { get; set; }
    public virtual string PromptText { get; set; }
    public virtual bool IsActive { get; set; }
    public virtual int TrafficWeight { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
}
