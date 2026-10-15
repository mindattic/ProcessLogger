using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MindAttic.Log.Extensions;
using MindAttic.Log.Migration;
using MindAttic.Log.Schema;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Tests;

/// <summary>
/// Proves the MindAttic.Log integration added to ProcessLogger.Cli's Program.cs writes through the
/// ordinary ILogger&lt;T&gt; call sites every store already uses, into its own standalone database
/// — NOT into processlogger.db (Phase 2, MindAttic.Log 3.0.0; Phase 1 shared processlogger.db
/// itself, which is superseded — see MindAttic.Log's docs/BIBLE.md §1.1) — and that Phase 1's rows
/// migrate across.
/// </summary>
public class MindAtticLogIntegrationTests
{
    [Test]
    public void AddMindAtticLog_Writes_Into_A_Standalone_Database_Not_ProcessLogger_Own_File()
    {
        var path = Path.Combine(Path.GetTempPath(), $"processlogger-log-tests-{Guid.NewGuid():N}.db");
        var standaloneDbPath = Path.Combine(Path.GetTempPath(), $"processlogger-standalone-log-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = new ProcessLoggerDbContext(ProcessLoggerDbContext.BuildOptions(path)))
                db.Database.Migrate(); // same call ProcessLoggerHostedService makes before watchers start

            var services = new ServiceCollection();
            services.AddMindAtticLog(o =>
            {
                o.Application = "ProcessLogger";
                o.DbPath = standaloneDbPath;
            });

            using (var provider = services.BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILogger<MindAtticLogIntegrationTests>>();
                logger.LogWarning("Integration test wrote this at {Utc}", DateTime.UtcNow);
            } // disposing flushes the batched sink (AddSerilog(..., dispose: true))

            SqliteConnection.ClearAllPools();

            using (var ownConnection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            {
                ownConnection.Open();
                using var tables = ownConnection.CreateCommand();
                tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                using var reader = tables.ExecuteReader();
                var names = new List<string>();
                while (reader.Read()) names.Add(reader.GetString(0));

                Assert.That(names, Does.Not.Contain(LogSchema.TableName), "processlogger.db must no longer gain a MindAttic_Log table (Phase 2).");
                Assert.That(names, Does.Contain("ProcessEvents"), "EF's own tables should be untouched.");
            }

            using var standaloneConnection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = standaloneDbPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
            standaloneConnection.Open();
            using var rows = standaloneConnection.CreateCommand();
            rows.CommandText = $"SELECT Application, Message FROM {LogSchema.TableName};";
            using var rowReader = rows.ExecuteReader();

            Assert.That(rowReader.Read(), Is.True, "Expected the logged warning to have reached the standalone database.");
            Assert.That(rowReader.GetString(0), Is.EqualTo("ProcessLogger"));
            Assert.That(rowReader.GetString(1), Does.Contain("Integration test wrote this"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
            if (File.Exists(standaloneDbPath)) File.Delete(standaloneDbPath);
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(standaloneDbPath + suffix)) File.Delete(standaloneDbPath + suffix);
        }
    }

    [Test]
    public async Task MigrateFromSqliteAsync_Carries_Phase1_Rows_Out_Of_ProcessLogger_Db_Into_The_Standalone_Database()
    {
        var path = Path.Combine(Path.GetTempPath(), $"processlogger-log-tests-{Guid.NewGuid():N}.db");
        var standaloneDbPath = Path.Combine(Path.GetTempPath(), $"processlogger-standalone-log-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = new ProcessLoggerDbContext(ProcessLoggerDbContext.BuildOptions(path)))
                db.Database.Migrate();

            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
            {
                connection.Open();
                using var create = connection.CreateCommand();
                create.CommandText = LogSchema.CreateTableSqlite;
                create.ExecuteNonQuery();
                using var insert = connection.CreateCommand();
                insert.CommandText = $"""
                    INSERT INTO {LogSchema.TableName} (TimestampUtc, Level, Application, Message)
                    VALUES ('2026-01-01T00:00:00.000Z', 2, 'ProcessLogger', 'Phase 1 row');
                    """;
                insert.ExecuteNonQuery();
            }
            SqliteConnection.ClearAllPools();

            var copied = await MindAtticLogMigrator.MigrateFromSqliteAsync(path, standaloneDbPath, dropSource: true);
            Assert.That(copied, Is.EqualTo(1));

            SqliteConnection.ClearAllPools();
            using (var ownConnection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString()))
            {
                ownConnection.Open();
                using var tables = ownConnection.CreateCommand();
                tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                using var reader = tables.ExecuteReader();
                var names = new List<string>();
                while (reader.Read()) names.Add(reader.GetString(0));
                Assert.That(names, Does.Not.Contain(LogSchema.TableName), "dropSource: true should have removed Phase 1's table.");
            }

            using var standaloneConnection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = standaloneDbPath, Mode = SqliteOpenMode.ReadOnly }.ToString());
            standaloneConnection.Open();
            using var rows = standaloneConnection.CreateCommand();
            rows.CommandText = $"SELECT Message FROM {LogSchema.TableName};";
            Assert.That(rows.ExecuteScalar(), Is.EqualTo("Phase 1 row"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
            if (File.Exists(standaloneDbPath)) File.Delete(standaloneDbPath);
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(standaloneDbPath + suffix)) File.Delete(standaloneDbPath + suffix);
        }
    }
}
