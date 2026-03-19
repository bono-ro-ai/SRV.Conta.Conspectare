using System.Collections;

namespace Conspectare.Infrastructure.NHibernate.Extensions;

public class BatchLoader
{
    private List<int> _ids = new();

    public static BatchLoader With(List<int> ids) => new() { _ids = ids };

    public List<TResult> BatchLoad<TResult>(Func<ICollection, IEnumerable<TResult>> loaderFunc,
        int batchSize = 100)
    {
        var results = new List<TResult>();
        if (!_ids.Any())
            return results;

        foreach (var batchIds in _ids.Batch(batchSize))
        {
            var batchResults = loaderFunc(batchIds);
            results.AddRange(batchResults);
        }

        return results;
    }
}

public static class EnumerableExtensions
{
    public static IEnumerable<List<T>> Batch<T>(this IEnumerable<T> collection, int batchSize)
    {
        var batch = new List<T>();

        foreach (var item in collection)
        {
            batch.Add(item);
            if (batch.Count != batchSize)
                continue;

            yield return batch;
            batch = new List<T>(batchSize);
        }

        if (batch.Count > 0)
            yield return batch;
    }
}
