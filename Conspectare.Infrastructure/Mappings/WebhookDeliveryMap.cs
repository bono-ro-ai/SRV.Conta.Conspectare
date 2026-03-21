using Conspectare.Domain.Entities;
using Conspectare.Infrastructure.Filters;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class WebhookDeliveryMap : ClassMap<WebhookDelivery>
{
    public WebhookDeliveryMap()
    {
        Table("pipe_webhook_deliveries");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.DocumentId).Column("document_id").Not.Nullable();
        Map(x => x.TenantId).Column("tenant_id").Not.Nullable();
        Map(x => x.WebhookUrl).Column("webhook_url").Not.Nullable().Length(500);
        Map(x => x.PayloadJson).Column("payload_json").CustomSqlType("LONGTEXT").Not.Nullable();
        Map(x => x.WebhookSecret).Column("webhook_secret").Length(128);
        Map(x => x.Status).Column("status").Not.Nullable().Length(30);
        Map(x => x.HttpStatusCode).Column("http_status_code");
        Map(x => x.ErrorMessage).Column("error_message").Length(2000);
        Map(x => x.AttemptCount).Column("attempt_count").Not.Nullable();
        Map(x => x.MaxAttempts).Column("max_attempts").Not.Nullable();
        Map(x => x.NextAttemptAt).Column("next_attempt_at");
        Map(x => x.LastAttemptAt).Column("last_attempt_at");
        Map(x => x.DeliveredAt).Column("delivered_at");
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.UpdatedAt).Column("updated_at").Not.Nullable();

        ApplyFilter<TenantFilterDefinition>("tenant_id = :tenantId");
    }
}
