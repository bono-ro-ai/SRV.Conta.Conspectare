using Conspectare.Domain.Enums;
using Conspectare.Services.Core.Database;

namespace Conspectare.Services.Commands;

public class RecoverStaleDocumentsCommand(DateTime cutoff) : NHibernateConspectareCommand<int>
{
    /// <summary>
    /// Bulk-recovers documents that have been stuck in a processing state past the
    /// given <paramref name="cutoff"/> timestamp. Documents in <c>Triaging</c> are
    /// returned to <c>PendingTriage</c>. Documents in <c>Extracting</c> are either
    /// marked <c>Failed</c> (retry limit reached) or returned to
    /// <c>PendingExtraction</c> (retries remaining). Returns the total number of
    /// rows affected across all three UPDATE statements.
    /// </summary>
    protected override int OnExecute()
    {
        // Stale triage documents — unconditionally returned to the triage queue.
        var triagingCount = Session.CreateSQLQuery(
                "UPDATE pipe_documents SET status = :newStatus, updated_at = UTC_TIMESTAMP() " +
                "WHERE status = :staleStatus AND updated_at < :cutoff")
            .SetParameter("newStatus", DocumentStatus.PendingTriage)
            .SetParameter("staleStatus", DocumentStatus.Triaging)
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();

        // Stale extraction documents that have exhausted all retries — terminate as Failed.
        var extractingExhaustedCount = Session.CreateSQLQuery(
                "UPDATE pipe_documents SET status = :newStatus, updated_at = UTC_TIMESTAMP(), completed_at = UTC_TIMESTAMP() " +
                "WHERE status = :staleStatus AND updated_at < :cutoff AND retry_count >= max_retries")
            .SetParameter("newStatus", DocumentStatus.Failed)
            .SetParameter("staleStatus", DocumentStatus.Extracting)
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();

        // Stale extraction documents that still have retries remaining — re-queue for extraction.
        var extractingRetryCount = Session.CreateSQLQuery(
                "UPDATE pipe_documents SET status = :newStatus, updated_at = UTC_TIMESTAMP() " +
                "WHERE status = :staleStatus AND updated_at < :cutoff AND retry_count < max_retries")
            .SetParameter("newStatus", DocumentStatus.PendingExtraction)
            .SetParameter("staleStatus", DocumentStatus.Extracting)
            .SetParameter("cutoff", cutoff)
            .ExecuteUpdate();

        return triagingCount + extractingExhaustedCount + extractingRetryCount;
    }
}
