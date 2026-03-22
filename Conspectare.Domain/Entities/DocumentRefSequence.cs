namespace Conspectare.Domain.Entities;

public class DocumentRefSequence
{
    public virtual long Id { get; set; }
    public virtual string FiscalCode { get; set; }
    public virtual int Year { get; set; }
    public virtual int LastSeq { get; set; }
}
