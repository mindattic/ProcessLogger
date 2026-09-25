using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Capture;

public enum ProcessLifecycleKind
{
    Started,
    Stopped,
}

public sealed record ProcessLifecycleEvent(
    ProcessLifecycleKind Kind,
    int ProcessId,
    int? ParentProcessId,
    string Name,
    string? Path,
    string? CommandLine,
    DateTime TimestampUtc,
    CaptureSource Source);
