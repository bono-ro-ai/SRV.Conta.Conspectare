using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class RefreshTokenMap : ClassMap<RefreshToken>
{
    public RefreshTokenMap()
    {
        Table("sec_refresh_tokens");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.UserId).Column("user_id").Not.Nullable();
        Map(x => x.TokenHash).Column("token_hash").Not.Nullable().Length(128).UniqueKey("ux_sec_refresh_tokens_token_hash");
        Map(x => x.ExpiresAt).Column("expires_at").Not.Nullable();
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.RevokedAt).Column("revoked_at");
        Map(x => x.ReplacedByTokenId).Column("replaced_by_token_id");

        References(x => x.User).Column("user_id").Not.Insert().Not.Update().ReadOnly();
    }
}
