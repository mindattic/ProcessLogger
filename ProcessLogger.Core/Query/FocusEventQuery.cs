namespace ProcessLogger.Core.Query;

public enum FocusEventSortColumn
{
    FocusedAtUtc,
    FocusLostAtUtc,
    ProcessName,
}

public sealed record FocusEventQuery(
    TextFilter? Include = null,
    TextFilter? Exclude = null,
    int Take = 100,
    FocusEventSortColumn SortBy = FocusEventSortColumn.FocusedAtUtc,
    bool Descending = true);
