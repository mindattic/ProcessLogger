using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcessLogger.Core.Capture;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Focus;

namespace ProcessLogger.Cli;

internal sealed class ProcessLoggerHostedService(
    IDbContextFactory<ProcessLoggerDbContext> dbContextFactory,
    ProcessWatcherService processWatcher,
    FocusWatcherService focusWatcher,
    ILogger<ProcessLoggerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using (var db = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            await db.Database.MigrateAsync(cancellationToken);
            await db.ApplyWalModeAsync();
        }

        processWatcher.Start();
        focusWatcher.Start();

        logger.LogInformation("ProcessLogger is watching. Press Ctrl+C to stop.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        processWatcher.Stop();
        focusWatcher.Stop();
        return Task.CompletedTask;
    }
}
