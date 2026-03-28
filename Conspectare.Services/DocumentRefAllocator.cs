using Conspectare.Services.Interfaces;
using ISession = NHibernate.ISession;

namespace Conspectare.Services;

/// <summary>
/// Allocates unique, human-readable document reference numbers per tenant fiscal code and year.
/// Format: <c>{fiscal_code}-{2-digit-year}-{sequence}</c> (e.g. <c>12345678-25-42</c>).
/// </summary>
public class DocumentRefAllocator : IDocumentRefAllocator
{
    private const string FallbackFiscalCode = "007";

    /// <summary>
    /// Atomically increments the per-fiscal-code/year sequence counter and returns the newly
    /// allocated reference string. Uses an <c>INSERT … ON DUPLICATE KEY UPDATE</c> to ensure
    /// the increment and read are race-free without a separate application-level lock.
    /// </summary>
    public async Task<string> AllocateRefAsync(ISession session, string fiscalCode)
    {
        var normalized = NormalizeFiscalCode(fiscalCode);
        // Store the year as a 2-digit value to keep references compact.
        var year = DateTime.UtcNow.Year % 100;

        // Atomically insert the first sequence row or increment an existing one.
        var upsertSql = @"
            INSERT INTO cfg_document_ref_sequences (fiscal_code, year, last_seq)
            VALUES (:fc, :yr, 1)
            ON DUPLICATE KEY UPDATE last_seq = last_seq + 1";

        await session.CreateSQLQuery(upsertSql)
            .SetParameter("fc", normalized)
            .SetParameter("yr", year)
            .ExecuteUpdateAsync();

        // Read back the sequence value that was just assigned to this allocation.
        var seq = await session.CreateSQLQuery(
                "SELECT last_seq FROM cfg_document_ref_sequences WHERE fiscal_code = :fc AND year = :yr")
            .SetParameter("fc", normalized)
            .SetParameter("yr", year)
            .UniqueResultAsync<int>();

        return $"{normalized}-{year}-{seq}";
    }

    /// <summary>
    /// Normalises a fiscal code by stripping the "RO" VAT prefix and surrounding whitespace.
    /// Returns <see cref="FallbackFiscalCode"/> when the input is null, empty, or whitespace-only.
    /// </summary>
    internal static string NormalizeFiscalCode(string fiscalCode)
    {
        if (string.IsNullOrWhiteSpace(fiscalCode))
            return FallbackFiscalCode;

        var trimmed = fiscalCode.Trim();

        // Strip the Romanian VAT prefix ("RO") if present so the ref uses only digits.
        if (trimmed.StartsWith("RO", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(2);

        trimmed = trimmed.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? FallbackFiscalCode : trimmed;
    }
}
