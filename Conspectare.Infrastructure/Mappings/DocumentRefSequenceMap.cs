using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class DocumentRefSequenceMap : ClassMap<DocumentRefSequence>
{
    public DocumentRefSequenceMap()
    {
        Table("cfg_document_ref_sequences");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.FiscalCode).Column("fiscal_code").Length(20).Not.Nullable();
        Map(x => x.Year).Column("year").Not.Nullable();
        Map(x => x.LastSeq).Column("last_seq").Not.Nullable();
    }
}
