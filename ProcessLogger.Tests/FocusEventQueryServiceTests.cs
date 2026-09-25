using Microsoft.EntityFrameworkCore;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Query;

namespace ProcessLogger.Tests;

public class FocusEventQueryServiceTests
{
    private TestDbContextFactory _dbFactory = null!;
    private FocusEventQueryService _service = null!;

    [SetUp]
    public async Task SetUp()
    {
        _dbFactory = new TestDbContextFactory();
        _service = new FocusEventQueryService(_dbFactory);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await using var db = await _dbFactory.CreateDbContextAsync();
        db.FocusEvents.AddRange(
            new FocusEvent { ProcessId = 1, ProcessName = "Game.exe", WindowTitle = "My Game", FocusedAtUtc = baseTime },
            new FocusEvent { ProcessId = 2, ProcessName = "WindowsTerminal.exe", WindowTitle = "Terminal", FocusedAtUtc = baseTime.AddSeconds(1), FocusLostAtUtc = baseTime.AddSeconds(3) });
        await db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown() => _dbFactory.Dispose();

    [Test]
    public async Task Include_PartialText_FiltersToMatchingRow()
    {
        var results = await _service.QueryAsync(new FocusEventQuery(Include: new TextFilter("terminal")));

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].ProcessName, Is.EqualTo("WindowsTerminal.exe"));
    }

    [Test]
    public async Task Exclude_RemovesMatchingRow()
    {
        var results = await _service.QueryAsync(new FocusEventQuery(Exclude: new TextFilter("terminal")));

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].ProcessName, Is.EqualTo("Game.exe"));
    }

    [Test]
    public async Task SortByFocusedAtUtc_Descending_ReturnsMostRecentFirst()
    {
        var results = await _service.QueryAsync(new FocusEventQuery(SortBy: FocusEventSortColumn.FocusedAtUtc, Descending: true));

        Assert.That(results.Select(r => r.ProcessName), Is.EqualTo(new[] { "WindowsTerminal.exe", "Game.exe" }));
    }
}
