using Microsoft.EntityFrameworkCore;

namespace ProcessLogger.Core.Data;

public sealed class ProcessLoggerDbContext(DbContextOptions<ProcessLoggerDbContext> options)
    : DbContext(options)
{
    public DbSet<ProcessEvent> ProcessEvents => Set<ProcessEvent>();
    public DbSet<FocusEvent> FocusEvents => Set<FocusEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessEvent>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.OpenedUtc);
        });

        modelBuilder.Entity<FocusEvent>(entity =>
        {
            entity.HasIndex(e => e.FocusedAtUtc);
        });
    }

    /// <summary>
    /// SQLite's journal mode is a persistent property of the database file, not a connection
    /// string setting Microsoft.Data.Sqlite recognizes, so it must be switched via PRAGMA. Once
    /// set it stays WAL for the file's lifetime; calling this again is a harmless no-op. WAL lets
    /// the Cli (writer) and the Frontend (reader) run concurrently without lock contention.
    /// </summary>
    public Task ApplyWalModeAsync() => Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

    public static DbContextOptions<ProcessLoggerDbContext> BuildOptions(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new DbContextOptionsBuilder<ProcessLoggerDbContext>();
        builder.UseSqlite($"Data Source={databasePath}");
        return builder.Options;
    }
}
