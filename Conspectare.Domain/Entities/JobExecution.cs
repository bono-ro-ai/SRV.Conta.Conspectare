namespace Conspectare.Domain.Entities;

public class JobExecution
{
    public virtual long Id { get; set; }
    public virtual string JobName { get; set; }
    public virtual string InstanceId { get; set; }
    public virtual DateTime StartedAt { get; set; }
    public virtual DateTime? CompletedAt { get; set; }
    public virtual int? DurationMs { get; set; }
    public virtual string Status { get; set; }
    public virtual int? ItemsProcessed { get; set; }
    public virtual string ErrorMessage { get; set; }
    public virtual DateTime CreatedAt { get; set; }
}
