using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Capture;

/// <summary>Mutable, in-flight record of a process seen by one or both capture sources, pending
/// (or already past) the reconcile-and-persist step. Not thread-safe on its own — callers must
/// hold <see cref="Gate"/> while reading or mutating the merge fields.</summary>
internal sealed class PendingProcess
{
    public readonly object Gate = new();

    public int ProcessId { get; }
    public DateTime OpenedUtc { get; }

    public int? ParentProcessId { get; private set; }
    public string Name { get; private set; }
    public string? Path { get; private set; }
    public string? CommandLine { get; private set; }
    public CaptureSource SourceSeen { get; private set; }

    /// <summary>Set once the DB insert is in flight/complete; all callers await the same task so
    /// exactly one row is ever inserted regardless of how many sources/timers race to persist it.</summary>
    public Task<long>? PersistTask { get; set; }

    public PendingProcess(ProcessLifecycleEvent evt)
    {
        ProcessId = evt.ProcessId;
        OpenedUtc = evt.TimestampUtc;
        ParentProcessId = evt.ParentProcessId;
        Name = evt.Name;
        Path = evt.Path;
        CommandLine = evt.CommandLine;
        SourceSeen = evt.Source;
    }

    /// <summary>Must be called under <see cref="Gate"/>.</summary>
    public void Merge(ProcessLifecycleEvent evt)
    {
        SourceSeen |= evt.Source;
        ParentProcessId ??= evt.ParentProcessId;
        Path ??= evt.Path;
        CommandLine ??= evt.CommandLine;
    }
}
