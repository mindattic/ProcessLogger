namespace ProcessLogger.Core.Query;

public enum ProcessEventSortColumn
{
    OpenedUtc,
    ClosedUtc,
    DurationMs,
    Name,
    PeakWorkingSetBytes,
}

public sealed record ProcessEventQuery(
    TextFilter? Include = null,
    TextFilter? Exclude = null,
    int Take = 100,
    ProcessEventSortColumn SortBy = ProcessEventSortColumn.OpenedUtc,
    bool Descending = true);
