using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MindAttic.Log;
using MindAttic.Log.Extensions;
using MindAttic.Log.Schema;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Tests;

/// <summary>
/// Proves the MindAttic.Log integration added to ProcessLogger.Cli's Program.cs actually writes
/// into the same processlogger.db file EF owns, alongside its own tables - not just that the
/// package reference compiles.
/// </summary>
public class MindAtticLogIntegrationTests
{
    [Test]
    public void AddMindAtticLog_Writes_Into_The_Same_File_As_ProcessLogger_Own_Tables()
    {
        var path = Path.Combine(Path.GetTempPath(), $"processlogger-log-tests-{Guid.NewGuid():N}.db");
        try
        {
            using (var db = new ProcessLoggerDbContext(ProcessLoggerDbContext.BuildOptions(path)))
                db.Database.Migrate(); // same call ProcessLoggerHostedService makes before watchers start

            var services = new ServiceCollection();
            services.AddMindAtticLog(o =>
            {
                o.Application = "ProcessLogger";
                o.Destination = LogDestination.Sqlite;
                o.SqlitePath = path;
            });

            using (var provider = services.BuildServiceProvider())
            {
                var logger = provider.GetRequiredService<ILogger<MindAtticLogIntegrationTests>>();
                logger.LogWarning("Integration test wrote this at {Utc}", DateTime.UtcNow);
            } // disposing flushes the batched sink (AddSerilog(..., dispose: true))

            SqliteConnection.ClearAllPools();

            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString());
            connection.Open();

            using (var tables = connection.CreateCommand())
            {
                tables.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                using var reader = tables.ExecuteReader();
                var names = new List<string>();
                while (reader.Read()) names.Add(reader.GetString(0));

                Assert.That(names, Does.Contain(LogSchema.TableName), "MindAttic_Log table should exist in processlogger.db.");
                Assert.That(names, Does.Contain("ProcessEvents"), "EF's own tables should be untouched by the log sink.");
            }

            using var rows = connection.CreateCommand();
            rows.CommandText = $"SELECT Application, Message FROM {LogSchema.TableName};";
            using var rowReader = rows.ExecuteReader();

            Assert.That(rowReader.Read(), Is.True, "Expected the logged warning to have reached the table.");
            Assert.That(rowReader.GetString(0), Is.EqualTo("ProcessLogger"));
            Assert.That(rowReader.GetString(1), Does.Contain("Integration test wrote this"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}
