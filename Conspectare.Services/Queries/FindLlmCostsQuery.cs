using Conspectare.Domain.Entities;
using Conspectare.Services.Core.Database;
using NHibernate.Criterion;

namespace Conspectare.Services.Queries;

public class FindLlmCostsQuery(long tenantId, DateTime from, DateTime to)
    : NHibernateConspectareQuery<IList<LlmCostResult>>
{
    /// <summary>
    /// Returns per-model token usage and attempt counts for the specified tenant within the given
    /// date range. Attempts without a model ID (e.g. failed before dispatch) are excluded.
    /// Results are grouped by model so callers can compute cost per model independently.
    /// </summary>
    protected override IList<LlmCostResult> OnExecute()
    {
        // NHibernate alias trick: a null local is used purely to capture property names for projection aliases.
        LlmCostResult result = null;

        return Session.QueryOver<ExtractionAttempt>()
            .Where(a => a.TenantId == tenantId)
            .And(a => a.CreatedAt >= from)
            .And(a => a.CreatedAt <= to)
            .And(a => a.ModelId != null)
            .SelectList(list => list
                .SelectGroup(a => a.ModelId).WithAlias(() => result.ModelId)
                .SelectSum(a => a.InputTokens).WithAlias(() => result.TotalInputTokens)
                .SelectSum(a => a.OutputTokens).WithAlias(() => result.TotalOutputTokens)
                .SelectCount(a => a.Id).WithAlias(() => result.AttemptCount))
            .TransformUsing(NHibernate.Transform.Transformers.AliasToBean<LlmCostResult>())
            .List<LlmCostResult>();
    }
}

public class LlmCostResult
{
    public string ModelId { get; set; }
    public int? TotalInputTokens { get; set; }
    public int? TotalOutputTokens { get; set; }
    public int AttemptCount { get; set; }
}
