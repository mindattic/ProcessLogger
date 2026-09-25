using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Memory;
using ProcessLogger.Core.Options;

namespace ProcessLogger.Core.Capture;

/// <summary>
/// Owns whichever capture source(s) are enabled and reconciles their events: if both the ETW and
/// WMI sources report the same PID within <see cref="ProcessLoggerOptions.ReconcileWindowMs"/> of
/// each other, they're merged into a single row (<see cref="CaptureSource.Both"/>, preferring
/// ETW's richer path/command line); otherwise the row reflects whichever single source reported
/// it. The same PID-keyed matching closes the correct open row on stop instead of duplicating it.
/// </summary>
public sealed class ProcessWatcherService : IDisposable
{
    private readonly TimeSpan _reconcileWindow;
    private readonly IReadOnlyList<IProcessCaptureSource> _sources;
    private readonly IDbContextFactory<ProcessLoggerDbContext> _dbContextFactory;
    private readonly MemorySamplerService _memorySampler;
    private readonly ILogger<ProcessWatcherService> _logger;
    private readonly ConcurrentDictionary<int, PendingProcess> _active = new();

    public ProcessWatcherService(
        IEnumerable<IProcessCaptureSource> allSources,
        ProcessLoggerOptions options,
        IDbContextFactory<ProcessLoggerDbContext> dbContextFactory,
        MemorySamplerService memorySampler,
        ILogger<ProcessWatcherService> logger)
    {
        _sources = allSources.Where(s => options.EnabledSources.HasFlag(s.Source)).ToList();
        _reconcileWindow = TimeSpan.FromMilliseconds(options.ReconcileWindowMs);
        _dbContextFactory = dbContextFactory;
        _memorySampler = memorySampler;
        _logger = logger;

        if (_sources.Count == 0)
        {
            _logger.LogWarning("No capture sources are enabled; ProcessLogger will record nothing.");
        }
    }

    public void Start()
    {
        foreach (var source in _sources)
        {
            source.ProcessStarted += OnProcessStarted;
            source.ProcessStopped += OnProcessStopped;
            source.Start();
        }
    }

    private void OnProcessStarted(object? sender, ProcessLifecycleEvent evt)
    {
        var entry = new PendingProcess(evt);
        if (_active.TryAdd(evt.ProcessId, entry))
        {
            _ = ScheduleFinalizeAsync(entry);
            return;
        }

        if (!_active.TryGetValue(evt.ProcessId, out var existing))
        {
            return;
        }

        bool bothSeen;
        lock (existing.Gate)
        {
            existing.Merge(evt);
            bothSeen = existing.SourceSeen == CaptureSource.Both;
        }

        if (bothSeen)
        {
            _ = GetOrCreatePersistTask(existing);
        }
    }

    private void OnProcessStopped(object? sender, ProcessLifecycleEvent evt)
    {
        if (!_active.TryRemove(evt.ProcessId, out var entry))
        {
            // A stop for a process we never saw start — it was already running before this
            // watcher launched. Nothing to close out.
            return;
        }

        _ = HandleStopAsync(entry, evt);
    }

    private async Task ScheduleFinalizeAsync(PendingProcess entry)
    {
        await Task.Delay(_reconcileWindow);
        await GetOrCreatePersistTask(entry);
    }

    private async Task HandleStopAsync(PendingProcess entry, ProcessLifecycleEvent stopEvt)
    {
        var dbId = await GetOrCreatePersistTask(entry);
        var sample = _memorySampler.StopSampling(entry.ProcessId);

        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var row = await db.ProcessEvents.FindAsync(dbId);
            if (row is null)
            {
                return;
            }

            row.ClosedUtc = stopEvt.TimestampUtc;
            row.DurationMs = (stopEvt.TimestampUtc - entry.OpenedUtc).TotalMilliseconds;
            row.PeakWorkingSetBytes = sample.PeakWorkingSetBytes;
            row.PeakPrivateBytes = sample.PeakPrivateBytes;
            row.FinalWorkingSetBytes = sample.FinalWorkingSetBytes;
            row.SampleCount = sample.SampleCount;

            await db.SaveChangesAsync();

            _logger.LogInformation("- {Name} (PID {ProcessId}) ran {DurationMs:F0}ms, peak WS {PeakWorkingSetBytes} bytes, {SampleCount} samples",
                entry.Name, entry.ProcessId, row.DurationMs, row.PeakWorkingSetBytes, row.SampleCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record process exit for PID {ProcessId}.", entry.ProcessId);
        }
    }

    /// <summary>
    /// Lazily starts (once) and returns the shared insert task for this process, so every caller
    /// racing to finalize the same <see cref="PendingProcess"/> awaits one insert, not several.
    /// </summary>
    private Task<long> GetOrCreatePersistTask(PendingProcess entry)
    {
        lock (entry.Gate)
        {
            entry.PersistTask ??= PersistAsync(entry);
            return entry.PersistTask;
        }
    }

    private async Task<long> PersistAsync(PendingProcess entry)
    {
        string? parentName = null;
        if (entry.ParentProcessId is int parentPid)
        {
            try
            {
                parentName = Process.GetProcessById(parentPid).ProcessName;
            }
            catch (ArgumentException)
            {
                // Parent already exited by the time we looked it up — best-effort only.
            }
        }

        // Best-effort: a console/background process never gets a main window, and even a GUI one
        // may not have created its window yet by the time the reconcile window elapses.
        string? title = null;
        try
        {
            var mainWindowTitle = Process.GetProcessById(entry.ProcessId).MainWindowTitle;
            title = string.IsNullOrEmpty(mainWindowTitle) ? null : mainWindowTitle;
        }
        catch (ArgumentException)
        {
            // Already exited by the time we looked it up.
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync();
        var row = new ProcessEvent
        {
            ProcessId = entry.ProcessId,
            ParentProcessId = entry.ParentProcessId,
            ParentProcessName = parentName,
            Name = entry.Name,
            Title = title,
            Path = entry.Path,
            CommandLine = entry.CommandLine,
            Source = entry.SourceSeen,
            OpenedUtc = entry.OpenedUtc,
        };
        db.ProcessEvents.Add(row);
        await db.SaveChangesAsync();

        _memorySampler.StartSampling(entry.ProcessId);

        _logger.LogInformation("+ [{Source}] {Name} (PID {ProcessId}, parent {ParentName})",
            entry.SourceSeen, entry.Name, entry.ProcessId, parentName ?? "?");

        return row.Id;
    }

    public void Stop()
    {
        foreach (var source in _sources)
        {
            source.ProcessStarted -= OnProcessStarted;
            source.ProcessStopped -= OnProcessStopped;
            source.Stop();
        }
    }

    public void Dispose()
    {
        Stop();
        foreach (var source in _sources)
        {
            source.Dispose();
        }
    }
}
