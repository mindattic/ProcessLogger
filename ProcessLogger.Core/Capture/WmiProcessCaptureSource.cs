using System.Management;
using Microsoft.Extensions.Logging;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Interop;

namespace ProcessLogger.Core.Capture;

/// <summary>
/// Redundant cross-check against <see cref="EtwProcessCaptureSource"/> via
/// <c>Win32_ProcessStartTrace</c>/<c>Win32_ProcessStopTrace</c>. Known to have ~1-2s startup
/// latency and can occasionally drop rapid bursts, which is exactly why this isn't the sole
/// source — the reconciler in <see cref="ProcessWatcherService"/> merges whichever source(s)
/// actually reported a given process.
/// </summary>
public sealed class WmiProcessCaptureSource(ILogger<WmiProcessCaptureSource> logger) : IProcessCaptureSource
{
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;

    public CaptureSource Source => CaptureSource.Wmi;

    public event EventHandler<ProcessLifecycleEvent>? ProcessStarted;
    public event EventHandler<ProcessLifecycleEvent>? ProcessStopped;

    public void Start()
    {
        try
        {
            _startWatcher = new ManagementEventWatcher(new WqlEventQuery("Win32_ProcessStartTrace"));
            _startWatcher.EventArrived += OnStartTrace;
            _startWatcher.Start();

            _stopWatcher = new ManagementEventWatcher(new WqlEventQuery("Win32_ProcessStopTrace"));
            _stopWatcher.EventArrived += OnStopTrace;
            _stopWatcher.Start();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start WMI process trace watchers.");
        }
    }

    private void OnStartTrace(object sender, EventArrivedEventArgs e)
    {
        var pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
        var parentPid = Convert.ToInt32(e.NewEvent["ParentProcessID"]);
        var name = (string)e.NewEvent["ProcessName"];

        // Best-effort race against the process possibly already exiting by the time we ask.
        var path = NativeProcessMethods.TryGetFullImagePath(pid);

        ProcessStarted?.Invoke(this, new ProcessLifecycleEvent(
            ProcessLifecycleKind.Started, pid, parentPid, name, path, null, DateTime.UtcNow, CaptureSource.Wmi));
    }

    private void OnStopTrace(object sender, EventArrivedEventArgs e)
    {
        var pid = Convert.ToInt32(e.NewEvent["ProcessID"]);
        var parentPid = Convert.ToInt32(e.NewEvent["ParentProcessID"]);
        var name = (string)e.NewEvent["ProcessName"];

        ProcessStopped?.Invoke(this, new ProcessLifecycleEvent(
            ProcessLifecycleKind.Stopped, pid, parentPid, name, null, null, DateTime.UtcNow, CaptureSource.Wmi));
    }

    public void Stop()
    {
        _startWatcher?.Stop();
        _startWatcher?.Dispose();
        _startWatcher = null;

        _stopWatcher?.Stop();
        _stopWatcher?.Dispose();
        _stopWatcher = null;
    }

    public void Dispose() => Stop();
}
