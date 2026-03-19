using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class CanonicalOutputMap : ClassMap<CanonicalOutput>
{
    public CanonicalOutputMap()
    {
        Table("pipe_canonical_outputs");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.TenantId).Column("tenant_id").Not.Nullable();
        Map(x => x.SchemaVersion).Column("schema_version").Not.Nullable();
        Map(x => x.OutputJson).Column("output_json").Not.Nullable().CustomSqlType("LONGTEXT");
        Map(x => x.InvoiceNumber).Column("invoice_number");
        Map(x => x.IssueDate).Column("issue_date");
        Map(x => x.DueDate).Column("due_date");
        Map(x => x.SupplierCui).Column("supplier_cui");
        Map(x => x.CustomerCui).Column("customer_cui");
        Map(x => x.Currency).Column("currency");
        Map(x => x.TotalAmount).Column("total_amount").Precision(15).Scale(2);
        Map(x => x.VatAmount).Column("vat_amount").Precision(15).Scale(2);
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();

        Map(x => x.DocumentId).Column("document_id").Not.Insert().Not.Update();
        References(x => x.Document).Column("document_id").Not.Nullable().Unique();
    }
}
