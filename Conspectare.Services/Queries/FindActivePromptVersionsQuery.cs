using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindActivePromptVersionsQuery(string phase, string documentType)
    : NHibernateConspectareQuery<IList<PromptVersion>>
{
    protected override IList<PromptVersion> OnExecute()
    {
        var query = Session.QueryOver<PromptVersion>()
            .Where(p => p.Phase == phase)
            .And(p => p.IsActive == true);

        if (documentType != null)
            query.And(p => p.DocumentType == documentType);
        else
            query.And(Restrictions.IsNull(nameof(PromptVersion.DocumentType)));

        return query.List();
    }
}
