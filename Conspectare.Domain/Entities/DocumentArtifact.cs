namespace Conspectare.Domain.Entities;

public class DocumentArtifact
{
    public virtual long Id { get; set; }
    public virtual long DocumentId { get; set; }
    public virtual long TenantId { get; set; }
    public virtual string ArtifactType { get; set; }
    public virtual string S3Key { get; set; }
    public virtual string ContentType { get; set; }
    public virtual long? SizeBytes { get; set; }
    public virtual int RetentionDays { get; set; }
    public virtual DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; }
}
