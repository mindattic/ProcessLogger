using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Tests;

/// <summary>A throwaway file-backed SQLite database per test instance. File-backed (not
/// <c>:memory:</c>) because <see cref="IDbContextFactory{TContext}"/> hands out a fresh connection
/// per context, and separate in-memory connections would each see an empty database.</summary>
internal sealed class TestDbContextFactory : IDbContextFactory<ProcessLoggerDbContext>, IDisposable
{
    private readonly DbContextOptions<ProcessLoggerDbContext> _options;
    private readonly string _path;

    public TestDbContextFactory()
    {
        _path = Path.Combine(Path.GetTempPath(), $"processlogger-tests-{Guid.NewGuid():N}.db");
        _options = ProcessLoggerDbContext.BuildOptions(_path);

        using var db = new ProcessLoggerDbContext(_options);
        db.Database.EnsureCreated();
    }

    public ProcessLoggerDbContext CreateDbContext() => new(_options);

    public Task<ProcessLoggerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(CreateDbContext());

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }
}
