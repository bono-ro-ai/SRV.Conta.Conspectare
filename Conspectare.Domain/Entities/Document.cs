namespace Conspectare.Domain.Entities;

public class Document
{
    public virtual long Id { get; set; }
    public virtual long TenantId { get; set; }
    public virtual string ExternalRef { get; set; }
    public virtual string ContentHash { get; set; }
    public virtual string FileName { get; set; }
    public virtual string ContentType { get; set; }
    public virtual long FileSizeBytes { get; set; }
    public virtual string InputFormat { get; set; }
    public virtual string Status { get; set; }
    public virtual string DocumentType { get; set; }
    public virtual decimal? TriageConfidence { get; set; }
    public virtual bool? IsAccountingRelevant { get; set; }
    public virtual int RetryCount { get; set; }
    public virtual int MaxRetries { get; set; } = 3;
    public virtual string ErrorMessage { get; set; }
    public virtual string RawFileS3Key { get; set; }
    public virtual string DocumentRef { get; set; }
    public virtual string FiscalCode { get; set; }
    public virtual string ClientReference { get; set; }
    public virtual string Metadata { get; set; }
    public virtual DateTime CreatedAt { get; set; }
    public virtual DateTime UpdatedAt { get; set; }
    public virtual DateTime? CompletedAt { get; set; }

    public virtual ApiClient Tenant { get; set; }
    public virtual IList<DocumentArtifact> Artifacts { get; set; } = new List<DocumentArtifact>();
    public virtual IList<ExtractionAttempt> ExtractionAttempts { get; set; } = new List<ExtractionAttempt>();
    public virtual IList<ReviewFlag> ReviewFlags { get; set; } = new List<ReviewFlag>();
    public virtual CanonicalOutput CanonicalOutput { get; set; }
    public virtual IList<DocumentEvent> Events { get; set; } = new List<DocumentEvent>();
}
