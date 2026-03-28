using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Queries;

public class FindAnyUserExistsQuery : NHibernateConspectareQuery<bool>
{
    /// <summary>
    /// Returns true if at least one user record exists in the database.
    /// Typically used during first-run setup to determine whether initial seeding is required.
    /// </summary>
    protected override bool OnExecute()
    {
        return Session.QueryOver<User>()
            .RowCount() > 0;
    }
}
