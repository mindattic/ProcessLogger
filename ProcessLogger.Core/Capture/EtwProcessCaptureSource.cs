using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Extensions.Logging;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Capture;

/// <summary>
/// Subscribes to the classic NT Kernel Logger's Process trace via TraceEvent — this is the
/// reliable backbone for catching processes that live only a fraction of a second, since the
/// kernel itself raises the event synchronously as part of process creation/exit, not on a
/// polling cadence. Only one such session can exist system-wide; if another tool already owns it,
/// <see cref="Start"/> logs the failure and leaves this source inert (the WMI source is the
/// fallback for exactly this situation).
/// </summary>
public sealed class EtwProcessCaptureSource(ILogger<EtwProcessCaptureSource> logger) : IProcessCaptureSource
{
    private TraceEventSession? _session;
    private Thread? _processingThread;

    public CaptureSource Source => CaptureSource.Etw;

    public event EventHandler<ProcessLifecycleEvent>? ProcessStarted;
    public event EventHandler<ProcessLifecycleEvent>? ProcessStopped;

    public void Start()
    {
        if (TraceEventSession.IsElevated() != true)
        {
            logger.LogError("ETW kernel process trace requires administrator privileges; source disabled.");
            return;
        }

        try
        {
            _session = new TraceEventSession(KernelTraceEventParser.KernelSessionName);
            _session.EnableKernelProvider(KernelTraceEventParser.Keywords.Process);
            _session.Source.Kernel.ProcessStart += data => Raise(ProcessLifecycleKind.Started, data.ProcessID, data.ParentID, data.ProcessName, data.CommandLine, data.TimeStamp);
            _session.Source.Kernel.ProcessStop += data => Raise(ProcessLifecycleKind.Stopped, data.ProcessID, data.ParentID, data.ProcessName, data.CommandLine, data.TimeStamp);

            _processingThread = new Thread(() => _session.Source.Process())
            {
                IsBackground = true,
                Name = "ProcessLogger-Etw",
            };
            _processingThread.Start();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start the ETW kernel process trace session (likely already owned by another tool).");
            _session?.Dispose();
            _session = null;
        }
    }

    private void Raise(ProcessLifecycleKind kind, int processId, int parentId, string name, string? commandLine, DateTime timestamp)
    {
        var evt = new ProcessLifecycleEvent(
            kind,
            processId,
            parentId,
            name,
            ProcessPathHeuristics.TryExtractPathFromCommandLine(commandLine),
            commandLine,
            timestamp.ToUniversalTime(),
            CaptureSource.Etw);

        (kind == ProcessLifecycleKind.Started ? ProcessStarted : ProcessStopped)?.Invoke(this, evt);
    }

    public void Stop()
    {
        _session?.Dispose();
        _session = null;
    }

    public void Dispose() => Stop();
}
