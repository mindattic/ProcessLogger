using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MindAttic.Log;
using MindAttic.Log.Extensions;
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

// App-owned SQLite tier: the MindAttic_Log table lives in the SAME processlogger.db file EF
// already owns, not a second file, so it stays covered by whatever already backs up or follows
// that database. ILogger<T> call sites throughout Core are unchanged.
builder.Services.AddMindAtticLog(o =>
{
    o.Application = "ProcessLogger";
    o.Destination = LogDestination.Sqlite;
    o.SqlitePath = databasePath;
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
