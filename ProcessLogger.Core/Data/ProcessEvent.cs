namespace ProcessLogger.Core.Data;

/// <summary>
/// One process's lifecycle, from creation to exit. "How much memory it gave back" is not a
/// metric Windows exposes for a foreign process (that's an internal allocator concept only the
/// process itself could report) — <see cref="FinalWorkingSetBytes"/> is the closest observable
/// proxy: the last sampled working set immediately before the process exited.
/// </summary>
public sealed class ProcessEvent
{
    public long Id { get; set; }

    public int ProcessId { get; set; }
    public int? ParentProcessId { get; set; }
    public string? ParentProcessName { get; set; }

    public required string Name { get; set; }

    /// <summary>The main window's title bar text - the value shown in the window's upper-left -
    /// sampled shortly after the process starts. Best-effort: null for processes with no window
    /// (background/console-only processes) or if the window hadn't been created yet when sampled.</summary>
    public string? Title { get; set; }

    /// <summary>Best-effort: only reliably populated by the ETW source.</summary>
    public string? Path { get; set; }

    /// <summary>Best-effort: only the ETW source supplies this.</summary>
    public string? CommandLine { get; set; }

    public CaptureSource Source { get; set; }

    public DateTime OpenedUtc { get; set; }
    public DateTime? ClosedUtc { get; set; }
    public double? DurationMs { get; set; }

    public long? PeakWorkingSetBytes { get; set; }
    public long? PeakPrivateBytes { get; set; }

    /// <summary>Last working-set sample taken before the process exited.</summary>
    public long? FinalWorkingSetBytes { get; set; }

    /// <summary>0 means the process lived shorter than one sampler tick.</summary>
    public int SampleCount { get; set; }
}
