using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using ProcessLogger.Core.Interop;

namespace ProcessLogger.Core.Memory;

/// <summary>
/// Polls a running process's working-set/private-bytes counters at a short interval while it's
/// alive, tracking peak and last-known values. This is the only way to observe memory usage at
/// all — once a process exits, its memory counters are gone, so "how much it gave back" is
/// approximated by <see cref="MemorySample.FinalWorkingSetBytes"/>, the last sample before exit.
/// </summary>
public sealed class MemorySamplerService : IDisposable
{
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(50);
    private const int MaxSamples = 200;
    private static readonly uint CountersSize = (uint)Marshal.SizeOf<NativeProcessMethods.PROCESS_MEMORY_COUNTERS_EX>();

    private sealed class Tracker
    {
        public long PeakWorkingSet;
        public long PeakPrivate;
        public long LastWorkingSet;
        public int Count;
        public readonly CancellationTokenSource Cts = new();
    }

    private readonly ILogger<MemorySamplerService> _logger;
    private readonly ConcurrentDictionary<int, Tracker> _trackers = new();

    public MemorySamplerService(ILogger<MemorySamplerService> logger)
    {
        _logger = logger;
    }

    public void StartSampling(int processId)
    {
        var tracker = new Tracker();
        if (!_trackers.TryAdd(processId, tracker))
        {
            return;
        }

        _ = SampleLoopAsync(processId, tracker);
    }

    public MemorySample StopSampling(int processId)
    {
        if (!_trackers.TryRemove(processId, out var tracker))
        {
            return new MemorySample(null, null, null, 0);
        }

        tracker.Cts.Cancel();
        tracker.Cts.Dispose();

        return tracker.Count > 0
            ? new MemorySample(tracker.PeakWorkingSet, tracker.PeakPrivate, tracker.LastWorkingSet, tracker.Count)
            : new MemorySample(null, null, null, 0);
    }

    private async Task SampleLoopAsync(int processId, Tracker tracker)
    {
        var handle = NativeProcessMethods.OpenProcess(
            NativeProcessMethods.PROCESS_QUERY_LIMITED_INFORMATION | NativeProcessMethods.PROCESS_VM_READ, false, processId);
        if (handle == 0)
        {
            // Process already exited before we could open a handle at all — a short-lived process
            // race, not an error; SampleCount stays 0 for this row.
            return;
        }

        try
        {
            var token = tracker.Cts.Token;
            while (!token.IsCancellationRequested && tracker.Count < MaxSamples)
            {
                if (!NativeProcessMethods.GetProcessMemoryInfo(handle, out var counters, CountersSize))
                {
                    break;
                }

                var workingSet = (long)counters.WorkingSetSize;
                var privateBytes = (long)counters.PrivateUsage;

                Interlocked.Exchange(ref tracker.LastWorkingSet, workingSet);
                InterlockedMax(ref tracker.PeakWorkingSet, workingSet);
                InterlockedMax(ref tracker.PeakPrivate, privateBytes);
                Interlocked.Increment(ref tracker.Count);

                try
                {
                    await Task.Delay(SampleInterval, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Memory sampling loop for PID {ProcessId} ended unexpectedly.", processId);
        }
        finally
        {
            NativeProcessMethods.CloseHandle(handle);
        }
    }

    private static void InterlockedMax(ref long location, long value)
    {
        long current;
        do
        {
            current = Interlocked.Read(ref location);
            if (value <= current)
            {
                return;
            }
        } while (Interlocked.CompareExchange(ref location, value, current) != current);
    }

    public void Dispose()
    {
        foreach (var tracker in _trackers.Values)
        {
            tracker.Cts.Cancel();
            tracker.Cts.Dispose();
        }

        _trackers.Clear();
    }
}
