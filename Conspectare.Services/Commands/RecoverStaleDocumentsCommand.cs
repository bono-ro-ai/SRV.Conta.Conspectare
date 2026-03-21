using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RecoverStaleDocumentsCommand(DateTime cutoff) : NHibernateConspectareCommand<int>
{
    protected override int OnExecute()
    {
        var triagingCount = Session.CreateSQLQuery(
                "UPDATE pipe_documents SET status = :newStatus, updated_at = UTC_TIMESTAMP() " +
                "WHERE status = :staleStatus AND updated_at < :cutoff")
            .SetParameter("newStatus", DocumentStatus.PendingTriage)
            .SetParameter("staleStatus", DocumentStatus.Triaging)
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();
        var extractingCount = Session.CreateSQLQuery(
                "UPDATE pipe_documents SET status = :newStatus, updated_at = UTC_TIMESTAMP() " +
                "WHERE status = :staleStatus AND updated_at < :cutoff")
            .SetParameter("newStatus", DocumentStatus.PendingExtraction)
            .SetParameter("staleStatus", DocumentStatus.Extracting)
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();
        return triagingCount + extractingCount;
    }
}
