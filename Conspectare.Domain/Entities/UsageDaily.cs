namespace Conspectare.Domain.Entities;

public class UsageDaily
{
    public virtual long Id { get; set; }
    public virtual long TenantId { get; set; }
    public virtual DateTime UsageDate { get; set; }
    public virtual int DocumentsIngested { get; set; }
    public virtual int DocumentsProcessed { get; set; }
    public virtual long LlmInputTokens { get; set; }
    public virtual long LlmOutputTokens { get; set; }
    public virtual int LlmRequests { get; set; }
    public virtual long StorageBytes { get; set; }
    public virtual int ApiCalls { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
}
