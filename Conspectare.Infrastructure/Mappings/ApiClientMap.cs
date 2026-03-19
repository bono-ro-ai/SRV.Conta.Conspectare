using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class ApiClientMap : ClassMap<ApiClient>
{
    public ApiClientMap()
    {
        Table("cfg_api_clients");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.Name).Column("name").Not.Nullable().Length(255);
        Map(x => x.ApiKeyHash).Column("api_key_hash").Not.Nullable().Length(128);
        Map(x => x.ApiKeyPrefix).Column("api_key_prefix").Not.Nullable().Length(8);
        Map(x => x.IsActive).Column("is_active").Not.Nullable();
        Map(x => x.RateLimitPerMin).Column("rate_limit_per_min").Not.Nullable();
        Map(x => x.MaxFileSizeMb).Column("max_file_size_mb").Not.Nullable();
        Map(x => x.WebhookUrl).Column("webhook_url").Length(500);
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.UpdatedAt).Column("updated_at").Not.Nullable();
    }
}
