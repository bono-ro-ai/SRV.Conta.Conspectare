namespace Conspectare.Domain.Entities;

public class CanonicalOutput
{
    public virtual long Id { get; set; }
    public virtual long DocumentId { get; set; }
    public virtual long TenantId { get; set; }
    public virtual string SchemaVersion { get; set; }
    public virtual string OutputJson { get; set; }
    public virtual string InvoiceNumber { get; set; }
    public virtual DateTime? IssueDate { get; set; }
    public virtual DateTime? DueDate { get; set; }
    public virtual string SupplierCui { get; set; }
    public virtual string CustomerCui { get; set; }
    public virtual string Currency { get; set; }
    public virtual decimal? TaxExclusiveAmount { get; set; }
    public virtual decimal? TaxInclusiveAmount { get; set; }
    public virtual decimal? VatAmount { get; set; }
    public virtual decimal? Discount { get; set; }
    public virtual string TaxNote { get; set; }
    public virtual string TaxCategory { get; set; }
    public virtual string SwiftBic { get; set; }
    public virtual string ConsensusStrategy { get; set; }
    public virtual string WinningModelId { get; set; }
    public virtual DateTime CreatedAt { get; set; }

    public virtual Document Document { get; set; }
}
