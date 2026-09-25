using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Query;

public sealed class FocusEventQueryService(IDbContextFactory<ProcessLoggerDbContext> dbContextFactory)
{
    /// <summary>All distinct raw values of one column across every logged row, for populating a
    /// column's "known values" filter list independent of whatever Take/Include/Exclude is active.</summary>
    public async Task<IReadOnlyList<TValue>> GetDistinctValuesAsync<TValue>(
        Expression<Func<FocusEvent, TValue>> selector, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.FocusEvents.AsNoTracking().Select(selector).Distinct().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FocusEvent>> QueryAsync(FocusEventQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<FocusEvent> results = db.FocusEvents.AsNoTracking();

        results = query.SortBy switch
        {
            FocusEventSortColumn.ProcessName => query.Descending
                ? results.OrderByDescending(e => e.ProcessName!.ToLower())
                : results.OrderBy(e => e.ProcessName!.ToLower()),
            FocusEventSortColumn.FocusLostAtUtc => query.Descending
                ? results.OrderByDescending(e => e.FocusLostAtUtc)
                : results.OrderBy(e => e.FocusLostAtUtc),
            _ => query.Descending
                ? results.OrderByDescending(e => e.FocusedAtUtc)
                : results.OrderBy(e => e.FocusedAtUtc),
        };

        if (!query.Include.IsSet() && !query.Exclude.IsSet())
        {
            return await results.Take(query.Take).ToListAsync(cancellationToken);
        }

        // Include/exclude support regex, which SQLite/EF can't translate to SQL, so the
        // filtered set is matched in memory once the ordered rows are materialized.
        var ordered = await results.ToListAsync(cancellationToken);
        return ordered
            .Where(e => Passes(e.ProcessName, query.Include, query.Exclude))
            .Take(query.Take)
            .ToList();
    }

    private static bool Passes(string? value, TextFilter? include, TextFilter? exclude)
    {
        if (include.IsSet() && !include.Matches(value))
        {
            return false;
        }

        return !exclude.Matches(value);
    }
}
