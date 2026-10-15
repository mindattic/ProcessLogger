using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MindAttic.Log.Extensions;
using MindAttic.Log.Migration;
using MindAttic.Vault.Paths;
using ProcessLogger.Cli;
using ProcessLogger.Core.Capture;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Focus;
using ProcessLogger.Core.Memory;
using ProcessLogger.Core.Options;

var builder = Host.CreateApplicationBuilder(args);

var options = new ProcessLoggerOptions();
builder.Configuration.GetSection(ProcessLoggerOptions.SectionName).Bind(options);
var databasePath = options.DatabasePath ?? ProcessLoggerOptions.DefaultDatabasePath;
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

builder.Services.AddSingleton(options);
builder.Services.AddPooledDbContextFactory<ProcessLoggerDbContext>(o => o.UseSqlite($"Data Source={databasePath}"));

// Phase 2 (MindAttic.Log 3.0.0, see its docs/BIBLE.md §1.1): logs now live in their own standalone
// MindAttic.Log.db under Vault's shared roaming root, not processlogger.db — Phase 1 shared the
// same file EF owns; that's superseded. ILogger<T> call sites throughout Core are unchanged.
var logDbPath = VaultPaths.LoggingDbPath("ProcessLogger");

// One-time copy of Phase 1's MindAttic_Log rows out of processlogger.db, then drop that table.
// Idempotent and a clean no-op on every later startup.
await MindAtticLogMigrator.MigrateFromSqliteAsync(databasePath, logDbPath, dropSource: true);
SqliteConnection.ClearAllPools();

builder.Services.AddMindAtticLog(o =>
{
    o.Application = "ProcessLogger";
    o.DbPath = logDbPath;
});

builder.Services.AddSingleton<EtwProcessCaptureSource>();
builder.Services.AddSingleton<WmiProcessCaptureSource>();
builder.Services.AddSingleton<IProcessCaptureSource>(sp => sp.GetRequiredService<EtwProcessCaptureSource>());
builder.Services.AddSingleton<IProcessCaptureSource>(sp => sp.GetRequiredService<WmiProcessCaptureSource>());

builder.Services.AddSingleton<MemorySamplerService>();
builder.Services.AddSingleton<ProcessWatcherService>();
builder.Services.AddSingleton<FocusWatcherService>();

builder.Services.AddHostedService<ProcessLoggerHostedService>();

var host = builder.Build();
await host.RunAsync();
