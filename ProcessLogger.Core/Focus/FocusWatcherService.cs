using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Focus;

/// <summary>
/// Records every OS foreground-focus change (window title, owning process, timestamp) so it can
/// be cross-referenced against <see cref="ProcessEvent"/> rows — e.g. "Game.exe lost focus at T,
/// WindowsTerminal.exe (PID 1234) gained it" — which is what actually identifies a focus-stealing
/// culprit, not the process log alone.
/// </summary>
public sealed class FocusWatcherService : IDisposable
{
    private readonly IDbContextFactory<ProcessLoggerDbContext> _dbContextFactory;
    private readonly ILogger<FocusWatcherService> _logger;
    private readonly NativeFocusMethods.WinEventDelegate _winEventProc;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private Thread? _messageLoopThread;
    private nint _hook;
    private long? _openFocusEventId;
    private volatile bool _running;

    public FocusWatcherService(IDbContextFactory<ProcessLoggerDbContext> dbContextFactory, ILogger<FocusWatcherService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
        _winEventProc = OnWinEvent;
    }

    public void Start()
    {
        _running = true;
        _messageLoopThread = new Thread(RunMessageLoop)
        {
            IsBackground = true,
            Name = "ProcessLogger-Focus",
        };
        _messageLoopThread.Start();
    }

    private void RunMessageLoop()
    {
        _hook = NativeFocusMethods.SetWinEventHook(
            NativeFocusMethods.EVENT_SYSTEM_FOREGROUND,
            NativeFocusMethods.EVENT_SYSTEM_FOREGROUND,
            nint.Zero,
            _winEventProc,
            0,
            0,
            NativeFocusMethods.WINEVENT_OUTOFCONTEXT);

        if (_hook == nint.Zero)
        {
            _logger.LogError("Failed to register the foreground-focus WinEvent hook.");
            return;
        }

        // WINEVENT_OUTOFCONTEXT callbacks are delivered via this thread's message queue.
        while (_running && NativeFocusMethods.GetMessage(out var msg, nint.Zero, 0, 0) > 0)
        {
            NativeFocusMethods.TranslateMessage(ref msg);
            NativeFocusMethods.DispatchMessage(ref msg);
        }
    }

    private void OnWinEvent(nint hWinEventHook, uint eventType, nint hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (hwnd == nint.Zero)
        {
            return;
        }

        NativeFocusMethods.GetWindowThreadProcessId(hwnd, out var pid);
        var title = ReadWindowTitle(hwnd);

        string? processName = null;
        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException)
        {
            // Process already exited between the event firing and us looking it up.
        }

        _ = RecordFocusChangeAsync((int)pid, processName, title, DateTime.UtcNow);
    }

    private static string? ReadWindowTitle(nint hwnd)
    {
        var length = NativeFocusMethods.GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return null;
        }

        var builder = new StringBuilder(length + 1);
        NativeFocusMethods.GetWindowText(hwnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private async Task RecordFocusChangeAsync(int processId, string? processName, string? windowTitle, DateTime timestampUtc)
    {
        await _gate.WaitAsync();
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();

            if (_openFocusEventId is long previousId)
            {
                var previous = await db.FocusEvents.FindAsync(previousId);
                if (previous is not null)
                {
                    previous.FocusLostAtUtc = timestampUtc;
                }
            }

            var entry = new FocusEvent
            {
                ProcessId = processId,
                ProcessName = processName,
                WindowTitle = windowTitle,
                FocusedAtUtc = timestampUtc,
            };
            db.FocusEvents.Add(entry);
            await db.SaveChangesAsync();
            _openFocusEventId = entry.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record a focus-change event.");
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Stop()
    {
        _running = false;
        if (_hook != nint.Zero)
        {
            NativeFocusMethods.UnhookWinEvent(_hook);
            _hook = nint.Zero;
        }
    }

    public void Dispose()
    {
        Stop();
        _gate.Dispose();
    }
}
