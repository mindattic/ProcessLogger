using Microsoft.Extensions.Configuration;
using MindAttic.Vault.Settings;
using MindAttic.Vault.Settings.Manifest;
using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Options;

/// <summary>
/// ProcessLogger's settings, per machine in <c>%LOCALAPPDATA%\MindAttic\ProcessLogger\settings.json</c>
/// and edited in MindAttic.Vault.Desktop. Read once at start; changes apply on the next start.
/// </summary>
[SettingsApp(AppId, "Process Logger", Local = true, Icon = "activity",
    Description = "Records which processes start, stop and take focus on this PC.")]
public sealed class ProcessLoggerOptions
{
    public const string AppId = "ProcessLogger";

    /// <summary>The <c>appsettings.json</c> section these settings were read from before LAMP; imported once.</summary>
    public const string SectionName = "ProcessLogger";

    /// <summary>Which capture source(s) to run. Defaults to both, reconciled against each other.</summary>
    [Setting("Capture sources", Group = "Capture", Order = 1,
        Help = "ETW (kernel trace: exact start times, paths and command lines; needs admin), WMI (no admin, coarser), or both reconciled into one row per process. None records nothing.")]
    public CaptureSource EnabledSources { get; set; } = CaptureSource.Both;

    /// <summary>
    /// Full path to the SQLite database file. Null uses the default
    /// <c>%LOCALAPPDATA%\MindAttic\ProcessLogger\processlogger.db</c>.
    /// </summary>
    [Setting("Database file", Group = "Storage", Order = 1,
        Help = "SQLite file the recorder writes and the viewer reads. Empty uses %LOCALAPPDATA%\\MindAttic\\ProcessLogger\\processlogger.db.")]
    [PathPicker(PathKind.File, Filter = "SQLite database|*.db|All files|*.*")]
    public string? DatabasePath { get; set; }

    /// <summary>
    /// How long <see cref="Capture.ProcessWatcherService"/> waits for a second capture source to
    /// confirm the same PID before persisting a single-source row. Tests shrink this to make the
    /// reconciler deterministic to await instead of racing a fixed real-time delay.
    /// </summary>
    [Setting("Reconcile window (ms)", Group = "Capture", Order = 2,
        Help = "How long to wait for the second source to report the same process before saving it from one source.")]
    [SettingRange(0, 10000)]
    public int ReconcileWindowMs { get; set; } = 250;

    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MindAttic", "ProcessLogger", "processlogger.db");

    /// <summary>The database the recorder and viewer use: <see cref="DatabasePath"/>, or the default when empty.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveDatabasePath => string.IsNullOrWhiteSpace(DatabasePath) ? DefaultDatabasePath : DatabasePath;

    /// <summary>
    /// Loads the Vault settings. On the first run after LAMP, values still configured in
    /// <paramref name="legacy"/> (the old <c>appsettings.json</c> section) are imported so nothing changes.
    /// </summary>
    public static ProcessLoggerOptions Load(IConfigurationSection? legacy = null, ISettingsStore<ProcessLoggerOptions>? store = null)
    {
        store ??= JsonSettingsStore<ProcessLoggerOptions>.ForSettingsApp();
        if (!store.Exists() && legacy is not null && legacy.Exists())
        {
            var imported = new ProcessLoggerOptions();
            legacy.Bind(imported);
            store.Save(imported);
        }
        return store.LoadOrSeed();
    }
}
