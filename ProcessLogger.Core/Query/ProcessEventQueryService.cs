using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Query;

public sealed class ProcessEventQueryService(IDbContextFactory<ProcessLoggerDbContext> dbContextFactory)
{
    /// <summary>All distinct raw values of one column across every logged row, for populating a
    /// column's "known values" filter list independent of whatever Take/Include/Exclude is active.</summary>
    public async Task<IReadOnlyList<TValue>> GetDistinctValuesAsync<TValue>(
        Expression<Func<ProcessEvent, TValue>> selector, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ProcessEvents.AsNoTracking().Select(selector).Distinct().ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessEvent>> QueryAsync(ProcessEventQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        IQueryable<ProcessEvent> results = db.ProcessEvents.AsNoTracking();

        results = query.SortBy switch
        {
            // SQLite's default ORDER BY collation is byte-wise (all uppercase before all
            // lowercase), which reads as broken to a user sorting a process list; ToLower()
            // translates to SQL lower() so the sort is ordinary case-insensitive alphabetical.
            ProcessEventSortColumn.Name => query.Descending
                ? results.OrderByDescending(e => e.Name.ToLower())
                : results.OrderBy(e => e.Name.ToLower()),
            ProcessEventSortColumn.ClosedUtc => query.Descending
                ? results.OrderByDescending(e => e.ClosedUtc)
                : results.OrderBy(e => e.ClosedUtc),
            ProcessEventSortColumn.DurationMs => query.Descending
                ? results.OrderByDescending(e => e.DurationMs)
                : results.OrderBy(e => e.DurationMs),
            ProcessEventSortColumn.PeakWorkingSetBytes => query.Descending
                ? results.OrderByDescending(e => e.PeakWorkingSetBytes)
                : results.OrderBy(e => e.PeakWorkingSetBytes),
            _ => query.Descending
                ? results.OrderByDescending(e => e.OpenedUtc)
                : results.OrderBy(e => e.OpenedUtc),
        };

        if (!query.Include.IsSet() && !query.Exclude.IsSet())
        {
            return await results.Take(query.Take).ToListAsync(cancellationToken);
        }

        // Include/exclude support regex, which SQLite/EF can't translate to SQL, so the
        // filtered set is matched in memory once the ordered rows are materialized.
        var ordered = await results.ToListAsync(cancellationToken);
        return ordered
            .Where(e => Passes(e.Name, query.Include, query.Exclude))
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
