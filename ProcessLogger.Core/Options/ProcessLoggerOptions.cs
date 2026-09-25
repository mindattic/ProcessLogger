using ProcessLogger.Core.Data;

namespace ProcessLogger.Core.Options;

public sealed class ProcessLoggerOptions
{
    public const string SectionName = "ProcessLogger";

    /// <summary>Which capture source(s) to run. Defaults to both, reconciled against each other.</summary>
    public CaptureSource EnabledSources { get; set; } = CaptureSource.Both;

    /// <summary>
    /// Full path to the SQLite database file. Null uses the default
    /// <c>%LOCALAPPDATA%\MindAttic\ProcessLogger\processlogger.db</c>.
    /// </summary>
    public string? DatabasePath { get; set; }

    /// <summary>
    /// How long <see cref="Capture.ProcessWatcherService"/> waits for a second capture source to
    /// confirm the same PID before persisting a single-source row. Tests shrink this to make the
    /// reconciler deterministic to await instead of racing a fixed real-time delay.
    /// </summary>
    public int ReconcileWindowMs { get; set; } = 250;

    public static string DefaultDatabasePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MindAttic", "ProcessLogger", "processlogger.db");
}
