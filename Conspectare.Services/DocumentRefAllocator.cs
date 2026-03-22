using Conspectare.Services.Interfaces;
using ISession = NHibernate.ISession;

namespace Conspectare.Services;

public class DocumentRefAllocator : IDocumentRefAllocator
{
    private const string FallbackFiscalCode = "007";

    public async Task<string> AllocateRefAsync(ISession session, string fiscalCode)
    {
        var normalized = NormalizeFiscalCode(fiscalCode);
        var year = DateTime.UtcNow.Year % 100;

        var upsertSql = @"
            INSERT INTO cfg_document_ref_sequences (fiscal_code, year, last_seq)
            VALUES (:fc, :yr, 1)
            ON DUPLICATE KEY UPDATE last_seq = last_seq + 1";

        await session.CreateSQLQuery(upsertSql)
            .SetParameter("fc", normalized)
            .SetParameter("yr", year)
            .ExecuteUpdateAsync();

        var seq = await session.CreateSQLQuery(
                "SELECT last_seq FROM cfg_document_ref_sequences WHERE fiscal_code = :fc AND year = :yr")
            .SetParameter("fc", normalized)
            .SetParameter("yr", year)
            .UniqueResultAsync<int>();

        return $"{normalized}-{year}-{seq}";
    }

    internal static string NormalizeFiscalCode(string fiscalCode)
    {
        if (string.IsNullOrWhiteSpace(fiscalCode))
            return FallbackFiscalCode;

        var trimmed = fiscalCode.Trim();

        if (trimmed.StartsWith("RO", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(2);

        trimmed = trimmed.Trim();

        return string.IsNullOrWhiteSpace(trimmed) ? FallbackFiscalCode : trimmed;
    }
}
