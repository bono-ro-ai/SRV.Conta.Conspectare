using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class MagicLinkTokenMap : ClassMap<MagicLinkToken>
{
    public MagicLinkTokenMap()
    {
        Table("sec_magic_link_tokens");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.UserId).Column("user_id");
        Map(x => x.TokenHash).Column("token_hash").Not.Nullable().Length(255).UniqueKey("ux_sec_magic_link_tokens_hash");
        Map(x => x.Email).Column("email").Not.Nullable().Length(255);
        Map(x => x.ExpiresAt).Column("expires_at").Not.Nullable();
        Map(x => x.UsedAt).Column("used_at");
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.IpAddress).Column("ip_address").Length(45);

        References(x => x.User).Column("user_id").Not.Insert().Not.Update().LazyLoad();
    }
}
