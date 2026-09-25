using ProcessLogger.Core.Capture;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Tests;

/// <summary>A controllable stand-in for <see cref="EtwProcessCaptureSource"/>/
/// <see cref="WmiProcessCaptureSource"/> so reconciler tests can fire start/stop events on demand
/// instead of depending on real kernel/WMI traces.</summary>
internal sealed class FakeCaptureSource(CaptureSource source) : IProcessCaptureSource
{
    public CaptureSource Source { get; } = source;

    public event EventHandler<ProcessLifecycleEvent>? ProcessStarted;
    public event EventHandler<ProcessLifecycleEvent>? ProcessStopped;

    public void Start()
    {
    }

    public void Stop()
    {
    }

    public void Dispose()
    {
    }

    public void RaiseStart(ProcessLifecycleEvent evt) => ProcessStarted?.Invoke(this, evt);

    public void RaiseStop(ProcessLifecycleEvent evt) => ProcessStopped?.Invoke(this, evt);
}
