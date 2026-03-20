namespace Conspectare.Domain.Entities;

public class ExtractionAttempt
{
    public virtual long Id { get; set; }
    public virtual long DocumentId { get; set; }
    public virtual long TenantId { get; set; }
    public virtual int AttemptNumber { get; set; }
    public virtual string Phase { get; set; }
    public virtual string ModelId { get; set; }
    public virtual string PromptVersion { get; set; }
    public virtual string Status { get; set; }
    public virtual int? InputTokens { get; set; }
    public virtual int? OutputTokens { get; set; }
    public virtual int? LatencyMs { get; set; }
    public virtual decimal? Confidence { get; set; }
    public virtual string ErrorMessage { get; set; }
    public virtual long? ResponseArtifactId { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime? CompletedAt { get; set; }

    public virtual string ProviderKey { get; set; }
    public virtual Document Document { get; set; }
}
