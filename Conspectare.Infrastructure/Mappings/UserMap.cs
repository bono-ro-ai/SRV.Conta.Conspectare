using Conspectare.Domain.Entities;
using FluentNHibernate.Mapping;

namespace Conspectare.Infrastructure.Mappings;

public class UserMap : ClassMap<User>
{
    public UserMap()
    {
        Table("sec_users");

        Id(x => x.Id).Column("id").GeneratedBy.Identity();

        Map(x => x.Email).Column("email").Not.Nullable().Length(255).UniqueKey("ux_sec_users_email");
        Map(x => x.Name).Column("name").Not.Nullable().Length(255);
        Map(x => x.PasswordHash).Column("password_hash").Not.Nullable().Length(255);
        Map(x => x.Role).Column("role").Not.Nullable().Length(50);
        Map(x => x.IsActive).Column("is_active").Not.Nullable();
        Map(x => x.FailedLoginAttempts).Column("failed_login_attempts").Not.Nullable();
        Map(x => x.LockedUntil).Column("locked_until");
        Map(x => x.LastLoginAt).Column("last_login_at");
        Map(x => x.CreatedAt).Column("created_at").Not.Nullable();
        Map(x => x.UpdatedAt).Column("updated_at").Not.Nullable();
    }
}
