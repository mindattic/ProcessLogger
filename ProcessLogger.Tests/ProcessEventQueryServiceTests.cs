using Microsoft.EntityFrameworkCore;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Query;

namespace ProcessLogger.Tests;

public class ProcessEventQueryServiceTests
{
    private TestDbContextFactory _dbFactory = null!;
    private ProcessEventQueryService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbFactory = new TestDbContextFactory();
        _service = new ProcessEventQueryService(_dbFactory);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.ProcessEvents.AddRange(
            new ProcessEvent { Name = "cmd.exe", ProcessId = 1, OpenedUtc = baseTime, PeakWorkingSetBytes = 1000 },
            new ProcessEvent { Name = "WindowsTerminal.exe", ProcessId = 2, OpenedUtc = baseTime.AddSeconds(1), PeakWorkingSetBytes = 3000 },
            new ProcessEvent { Name = "notepad.exe", ProcessId = 3, OpenedUtc = baseTime.AddSeconds(2), PeakWorkingSetBytes = 2000 });
        await db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _dbFactory.Dispose();

    [Test]
    public async Task Include_PartialText_FiltersCaseInsensitively()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(Include: new TextFilter("terminal")));

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Name, Is.EqualTo("WindowsTerminal.exe"));
    }

    [Test]
    public async Task Include_ExactString_OnlyMatchesWholeName()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(Include: new TextFilter("terminal", TextFilterMode.ExactString)));

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task Include_Regex_MatchesPattern()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(Include: new TextFilter("^(cmd|notepad)\\.exe$", TextFilterMode.Regex)));

        Assert.That(results.Select(r => r.Name), Is.EquivalentTo(new[] { "cmd.exe", "notepad.exe" }));
    }

    [Test]
    public async Task Exclude_PartialText_RemovesMatchingRows()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(Exclude: new TextFilter("terminal")));

        Assert.That(results.Select(r => r.Name), Is.EquivalentTo(new[] { "cmd.exe", "notepad.exe" }));
    }

    [Test]
    public async Task IncludeAndExclude_AreBothApplied()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(
            Include: new TextFilter(".exe", TextFilterMode.PartialText),
            Exclude: new TextFilter("notepad")));

        Assert.That(results.Select(r => r.Name), Is.EquivalentTo(new[] { "cmd.exe", "WindowsTerminal.exe" }));
    }

    [Test]
    public async Task Take_LimitsResultCount()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(Take: 2));

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task SortByOpenedUtc_Descending_ReturnsNewestFirst()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(SortBy: ProcessEventSortColumn.OpenedUtc, Descending: true));

        Assert.That(results.Select(r => r.Name), Is.EqualTo(new[] { "notepad.exe", "WindowsTerminal.exe", "cmd.exe" }));
    }

    [Test]
    public async Task SortByName_Ascending_ReturnsAlphabeticalOrder()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(SortBy: ProcessEventSortColumn.Name, Descending: false));

        Assert.That(results.Select(r => r.Name), Is.EqualTo(new[] { "cmd.exe", "notepad.exe", "WindowsTerminal.exe" }));
    }

    [Test]
    public async Task SortByPeakWorkingSet_Descending_ReturnsLargestFirst()
    {
        var results = await _service.QueryAsync(new ProcessEventQuery(SortBy: ProcessEventSortColumn.PeakWorkingSetBytes, Descending: true));

        Assert.That(results.Select(r => r.Name), Is.EqualTo(new[] { "WindowsTerminal.exe", "notepad.exe", "cmd.exe" }));
    }
}
