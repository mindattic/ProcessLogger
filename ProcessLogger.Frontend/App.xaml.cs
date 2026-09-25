using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Options;
using ProcessLogger.Core.Query;

namespace ProcessLogger.Frontend;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var databasePath = ProcessLoggerOptions.DefaultDatabasePath;
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);

        var services = new ServiceCollection();
        services.AddPooledDbContextFactory<ProcessLoggerDbContext>(o => o.UseSqlite($"Data Source={databasePath}"));
        services.AddSingleton<ProcessEventQueryService>();
        services.AddSingleton<FocusEventQueryService>();
        _services = services.BuildServiceProvider();

        var window = new MainWindow(
            _services.GetRequiredService<ProcessEventQueryService>(),
            _services.GetRequiredService<FocusEventQueryService>());
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }
}
