using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ProcessLogger.Core.Capture;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Memory;
using ProcessLogger.Core.Options;

namespace ProcessLogger.Tests;

public class ProcessWatcherServiceTests
{
    private static int _pidCounter = 100_000;

    private TestDbContextFactory _dbFactory = null!;
    private FakeCaptureSource _etw = null!;
    private FakeCaptureSource _wmi = null!;
    private MemorySamplerService _memorySampler = null!;
    private ProcessWatcherService _watcher = null!;

    [SetUp]
    public void SetUp()
    {
        _dbFactory = new TestDbContextFactory();
        _etw = new FakeCaptureSource(CaptureSource.Etw);
        _wmi = new FakeCaptureSource(CaptureSource.Wmi);
        _memorySampler = new MemorySamplerService(NullLogger<MemorySamplerService>.Instance);

        var options = new ProcessLoggerOptions { ReconcileWindowMs = 30 };
        _watcher = new ProcessWatcherService(
            [_etw, _wmi],
            options,
            _dbFactory,
            _memorySampler,
            NullLogger<ProcessWatcherService>.Instance);
        _watcher.Start();
    }

    [TearDown]
    public void TearDown()
    {
        _watcher.Dispose();
        _etw.Dispose();
        _wmi.Dispose();
        _memorySampler.Dispose();
        _dbFactory.Dispose();
    }

    [Test]
    public async Task SingleSource_PersistsRowWithThatSource()
    {
        var pid = NextPid();
        var opened = DateTime.UtcNow;
        _etw.RaiseStart(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Started, pid, null, "cmd.exe", @"C:\Windows\System32\cmd.exe",
            @"C:\Windows\System32\cmd.exe", opened, CaptureSource.Etw));

        await Task.Delay(150);

        var rows = await GetRowsAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Source, Is.EqualTo(CaptureSource.Etw));
        Assert.That(rows[0].Name, Is.EqualTo("cmd.exe"));
    }

    [Test]
    public async Task BothSources_ConsolidateIntoOneRow()
    {
        var pid = NextPid();
        var opened = DateTime.UtcNow;
        _etw.RaiseStart(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Started, pid, null, "cmd.exe", @"C:\Windows\System32\cmd.exe",
            @"C:\Windows\System32\cmd.exe /c dir", opened, CaptureSource.Etw));
        _wmi.RaiseStart(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Started, pid, null, "cmd.exe", null, null, opened, CaptureSource.Wmi));

        await Task.Delay(150);

        var rows = await GetRowsAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Source, Is.EqualTo(CaptureSource.Both));
        Assert.That(rows[0].CommandLine, Is.EqualTo(@"C:\Windows\System32\cmd.exe /c dir"));
    }

    [Test]
    public async Task BothSources_WmiFirst_StillPrefersEtwPath()
    {
        // Same scenario as BothSources_ConsolidateIntoOneRow, but with the arrival order flipped:
        // WMI (with its own resolved path) reports first, then ETW. The reconciler must still end
        // up with ETW's path - "preferring ETW's richer path" (see PendingProcess.Merge) shouldn't
        // depend on which source happened to arrive first.
        var pid = NextPid();
        var opened = DateTime.UtcNow;
        _wmi.RaiseStart(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Started, pid, null, "cmd.exe", @"C:\Windows\cmd.exe", null, opened, CaptureSource.Wmi));
        _etw.RaiseStart(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Started, pid, null, "cmd.exe", @"C:\Windows\System32\cmd.exe",
            @"C:\Windows\System32\cmd.exe /c dir", opened, CaptureSource.Etw));

        await Task.Delay(150);

        var rows = await GetRowsAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Source, Is.EqualTo(CaptureSource.Both));
        Assert.That(rows[0].Path, Is.EqualTo(@"C:\Windows\System32\cmd.exe"));
    }

    [Test]
    public async Task Stop_ClosesTheOpenRow()
    {
        var pid = NextPid();
        var opened = DateTime.UtcNow;
        _etw.RaiseStart(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Started, pid, null, "notepad.exe", null, null, opened, CaptureSource.Etw));
        await Task.Delay(60);

        var closed = opened.AddMilliseconds(500);
        _etw.RaiseStop(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Stopped, pid, null, "notepad.exe", null, null, closed, CaptureSource.Etw));

        await Task.Delay(150);

        var rows = await GetRowsAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].ClosedUtc, Is.EqualTo(closed).Within(TimeSpan.FromMilliseconds(5)));
        Assert.That(rows[0].DurationMs, Is.Not.Null);
    }

    [Test]
    public async Task VeryShortLived_StopBeforeReconcileWindow_StillPersistsExactlyOneRow()
    {
        var pid = NextPid();
        var opened = DateTime.UtcNow;
        _etw.RaiseStart(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Started, pid, null, "flash.exe", null, null, opened, CaptureSource.Etw));
        _etw.RaiseStop(new ProcessLifecycleEvent(
            ProcessLifecycleKind.Stopped, pid, null, "flash.exe", null, null, opened.AddMilliseconds(5), CaptureSource.Etw));

        await Task.Delay(150);

        var rows = await GetRowsAsync();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].ClosedUtc, Is.Not.Null);
    }

    private static int NextPid() => Interlocked.Increment(ref _pidCounter);

    private async Task<List<ProcessEvent>> GetRowsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.ProcessEvents.AsNoTracking().ToListAsync();
    }
}
