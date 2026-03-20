using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.Filters;
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
        Map(x => x.TaxExclusiveAmount).Column("tax_exclusive_amount").Precision(18).Scale(4);
        Map(x => x.TaxInclusiveAmount).Column("tax_inclusive_amount").Precision(18).Scale(4);
        Map(x => x.VatAmount).Column("vat_amount").Precision(18).Scale(4);
        Map(x => x.Discount).Column("discount").Precision(18).Scale(4);
        Map(x => x.TaxNote).Column("tax_note").CustomSqlType("TEXT");
        Map(x => x.TaxCategory).Column("tax_category").Length(50);
        Map(x => x.SwiftBic).Column("swift_bic").Length(11);
        Map(x => x.ConsensusStrategy).Column("consensus_strategy");
        Map(x => x.WinningModelId).Column("winning_model_id");
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();

        Map(x => x.DocumentId).Column("document_id").Not.Insert().Not.Update();
        References(x => x.Document).Column("document_id").Not.Nullable().Unique();

        ApplyFilter<TenantFilterDefinition>("tenant_id = :tenantId");
    }
}
