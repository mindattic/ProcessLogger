using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using MindAttic.Vault.Paths;
using MindAttic.Vault.Settings;
using MindAttic.Vault.Settings.Manifest;
using ProcessLogger.Core.Capture;
using ProcessLogger.Core.Data;
using ProcessLogger.Core.Memory;
using ProcessLogger.Core.Options;

namespace ProcessLogger.Tests;

/// <summary>Every test in this assembly sees temp Vault roots, so nothing can write the real AppData.</summary>
[SetUpFixture]
public sealed class TestVaultRoots
{
    private string root = "";

    [OneTimeSetUp]
    public void SetUp()
    {
        root = Path.Combine(Path.GetTempPath(), "ProcessLoggerVault-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable(VaultPaths.RoamingRootEnvVar, Path.Combine(root, "roaming"));
        Environment.SetEnvironmentVariable(VaultPaths.LocalRootEnvVar, Path.Combine(root, "local"));
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        Environment.SetEnvironmentVariable(VaultPaths.RoamingRootEnvVar, null);
        Environment.SetEnvironmentVariable(VaultPaths.LocalRootEnvVar, null);
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}

/// <summary>LAMP (PL-T31): ProcessLogger's settings in Vault, one end-to-end test per setting.</summary>
[NonParallelizable]
public class LampSettingsTests
{
    private static string Folder => VaultPaths.LocalApp(ProcessLoggerOptions.AppId);

    [SetUp]
    public void SetUp()
    {
        if (Directory.Exists(Folder)) Directory.Delete(Folder, recursive: true);
    }

    private static IConfigurationSection Legacy(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build().GetSection(ProcessLoggerOptions.SectionName);

    private static async Task<List<ProcessEvent>> RecordOneEtwStart(ProcessLoggerOptions options)
    {
        using var db = new TestDbContextFactory();
        using var etw = new FakeCaptureSource(CaptureSource.Etw);
        using var sampler = new MemorySamplerService(NullLogger<MemorySamplerService>.Instance);
        using var watcher = new ProcessWatcherService([etw], options, db, sampler, NullLogger<ProcessWatcherService>.Instance);
        watcher.Start();
        etw.RaiseStart(new ProcessLifecycleEvent(ProcessLifecycleKind.Started, 424242, null, "x.exe", null, null, DateTime.UtcNow, CaptureSource.Etw));
        await Task.Delay(options.ReconcileWindowMs + 200);
        await using var ctx = await db.CreateDbContextAsync();
        return await ctx.ProcessEvents.AsNoTracking().ToListAsync();
    }

    [Test]
    public async Task Setting_EnabledSources_ImportsRoundTripsAndChoosesTheCaptureSources()
    {
        var loaded = ProcessLoggerOptions.Load(Legacy(new() { ["ProcessLogger:EnabledSources"] = "Wmi" }));
        Assert.That(loaded.EnabledSources, Is.EqualTo(CaptureSource.Wmi), "appsettings value imported");
        Assert.That(ProcessLoggerOptions.Load().EnabledSources, Is.EqualTo(CaptureSource.Wmi), "persisted in Vault");

        JsonSettingsStore<ProcessLoggerOptions>.ForSettingsApp().Update(o => o.EnabledSources = CaptureSource.Etw);
        var options = ProcessLoggerOptions.Load();
        options.ReconcileWindowMs = 10;
        Assert.That(await RecordOneEtwStart(options), Has.Count.EqualTo(1), "ETW enabled → ETW events recorded");
        options.EnabledSources = CaptureSource.Wmi;
        Assert.That(await RecordOneEtwStart(options), Is.Empty, "ETW disabled → ETW events ignored");
    }

    [Test]
    public void Setting_DatabasePath_ImportsRoundTripsAndIsTheDatabaseUsed()
    {
        var custom = Path.Combine(Path.GetTempPath(), "pl-custom.db");
        Assert.That(ProcessLoggerOptions.Load(Legacy(new() { ["ProcessLogger:DatabasePath"] = custom })).EffectiveDatabasePath, Is.EqualTo(custom));
        JsonSettingsStore<ProcessLoggerOptions>.ForSettingsApp().Update(o => o.DatabasePath = "");
        Assert.That(ProcessLoggerOptions.Load().EffectiveDatabasePath, Is.EqualTo(ProcessLoggerOptions.DefaultDatabasePath), "empty uses the default");
        Assert.That(SettingsManifestBuilder.Build<ProcessLoggerOptions>().Find("MindAttic:Settings:ProcessLogger:databasePath")!.Kind, Is.EqualTo(SettingKind.Path));
    }

    [Test]
    public async Task Setting_ReconcileWindowMs_ImportsRoundTripsAndDelaysSingleSourceRows()
    {
        Assert.That(ProcessLoggerOptions.Load(Legacy(new() { ["ProcessLogger:ReconcileWindowMs"] = "40" })).ReconcileWindowMs, Is.EqualTo(40));
        JsonSettingsStore<ProcessLoggerOptions>.ForSettingsApp().Update(o => o.ReconcileWindowMs = 25);
        var options = ProcessLoggerOptions.Load();
        Assert.That(options.ReconcileWindowMs, Is.EqualTo(25));
        Assert.That(await RecordOneEtwStart(options), Has.Count.EqualTo(1));
        var field = SettingsManifestBuilder.Build<ProcessLoggerOptions>().Find("MindAttic:Settings:ProcessLogger:reconcileWindowMs")!;
        Assert.That((field.Min, field.Max), Is.EqualTo(((double?)0, (double?)10000)));
    }

    [Test]
    public void EverySettingHasItsOwnTest()
    {
        var tested = GetType().GetMethods().Select(m => m.Name).Where(n => n.StartsWith("Setting_")).ToList();
        foreach (var field in SettingsManifestBuilder.Build<ProcessLoggerOptions>().Fields)
            Assert.That(tested.Any(t => t.StartsWith("Setting_" + char.ToUpperInvariant(field.Property[0]) + field.Property[1..] + "_")), Is.True, field.Property);
    }

    [Test]
    public void ExistingVaultFile_IsNeverOverwrittenByTheLegacySection()
    {
        JsonSettingsStore<ProcessLoggerOptions>.ForSettingsApp().Save(new ProcessLoggerOptions { EnabledSources = CaptureSource.Etw });
        Assert.That(ProcessLoggerOptions.Load(Legacy(new() { ["ProcessLogger:EnabledSources"] = "Wmi" })).EnabledSources, Is.EqualTo(CaptureSource.Etw));
    }

    [Test]
    public void FreshMachine_SeedsTodaysDefaultsAndAPerMachineManifest()
    {
        var o = ProcessLoggerOptions.Load(Legacy(new()));
        Assert.That((o.EnabledSources, o.DatabasePath, o.ReconcileWindowMs), Is.EqualTo((CaptureSource.Both, (string?)null, 250)));
        var m = SettingsManifestFile.TryRead(Path.Combine(Folder, SettingsManifest.FileName))!;
        Assert.That(m.Scope, Is.EqualTo(SettingsManifest.LocalScope));
        Assert.That(m.Fields.All(f => !string.IsNullOrWhiteSpace(f.Help)), Is.True);
        Assert.That(m.Find("MindAttic:Settings:ProcessLogger:enabledSources")!.Choices!.Select(c => c.Label), Is.EqualTo(new[] { "None", "Etw", "Wmi", "Both" }));
        Assert.That(File.ReadAllText(Path.Combine(Folder, "settings.json")), Does.Not.Contain("effectiveDatabasePath"));
    }

    [Test]
    public void AppSettingsJson_NoLongerCarriesTheSection()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "ProcessLogger.Cli", "appsettings.json");
        Assert.That(File.ReadAllText(path), Does.Not.Contain("\"ProcessLogger\""));
    }
}
