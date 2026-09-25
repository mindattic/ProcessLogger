namespace ProcessLogger.Core.Data;

/// <summary>
/// Which capture backend(s) observed a process. <see cref="Both"/> means the ETW kernel-process
/// trace and the WMI process trace both reported the same process within the reconcile window.
/// </summary>
[Flags]
public enum CaptureSource
{
    None = 0,
    Etw = 1,
    Wmi = 2,
    Both = Etw | Wmi,
}
