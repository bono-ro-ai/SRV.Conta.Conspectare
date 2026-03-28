using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindActivePromptVersionsQuery(string phase, string documentType)
    : NHibernateConspectareQuery<IList<PromptVersion>>
{
    /// <summary>
    /// Returns all active prompt versions for the given processing phase.
    /// When <paramref name="documentType"/> is provided, only prompts scoped to that document type
    /// are returned; when it is null, only prompts with no document-type scope are returned.
    /// This distinction allows global fallback prompts to coexist with type-specific overrides.
    /// </summary>
    protected override IList<PromptVersion> OnExecute()
    {
        var query = Session.QueryOver<PromptVersion>()
            .Where(p => p.Phase == phase)
            .And(p => p.IsActive == true);

        if (documentType != null)
            // Caller supplied a specific document type — return only matching scoped prompts.
            query.And(p => p.DocumentType == documentType);
        else
            // No document type supplied — return only prompts that have no type restriction (null column).
            query.And(Restrictions.IsNull(nameof(PromptVersion.DocumentType)));

        return query.List();
    }
}
