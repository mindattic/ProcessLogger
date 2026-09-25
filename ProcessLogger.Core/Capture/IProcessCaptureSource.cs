using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Capture;

/// <summary>A backend that observes process creation/exit and raises lifecycle events for both.</summary>
public interface IProcessCaptureSource : IDisposable
{
    CaptureSource Source { get; }

    event EventHandler<ProcessLifecycleEvent>? ProcessStarted;
    event EventHandler<ProcessLifecycleEvent>? ProcessStopped;

    void Start();
    void Stop();
}
