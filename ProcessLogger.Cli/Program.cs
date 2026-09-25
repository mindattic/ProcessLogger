using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
