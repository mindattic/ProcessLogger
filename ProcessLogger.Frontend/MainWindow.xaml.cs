using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using MindAttic.Export.Artifacts;
using ProcessLogger.Core.Query;

namespace ProcessLogger.Frontend;

/// <summary>
/// Plain WebView2 + vanilla JS (wwwroot/index.html), bridged via WebMessageReceived/ExecuteScriptAsync
/// - same hosting pattern as Automata.App / JobHunt.App.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ProcessEventQueryService _processQueryService;
    private readonly FocusEventQueryService _focusQueryService;

    public MainWindow(ProcessEventQueryService processQueryService, FocusEventQueryService focusQueryService)
    {
        InitializeComponent();
        _processQueryService = processQueryService;
        _focusQueryService = focusQueryService;

        _ = InitializeBrowserAsync();
    }

    private async Task InitializeBrowserAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MindAttic", "ProcessLogger", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await Browser.EnsureCoreWebView2Async(env);

        var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "processlogger.local", wwwroot, CoreWebView2HostResourceAccessKind.Allow);
        Browser.CoreWebView2.WebMessageReceived += OnMessage;

#if DEBUG
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F12) Browser.CoreWebView2?.OpenDevToolsWindow();
        };
#endif

        Browser.CoreWebView2.Navigate("https://processlogger.local/index.html");
    }

    private async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        JsonNode? msg;
        try
        {
            msg = JsonNode.Parse(args.WebMessageAsJson);
        }
        catch (JsonException)
        {
            return;
        }

        if (msg is null) return;
        var action = msg["action"]?.GetValue<string>();

        try
        {
            switch (action)
            {
                case "queryProcesses":
                    await HandleQueryProcessesAsync(msg);
                    break;
                case "queryFocus":
                    await HandleQueryFocusAsync(msg);
                    break;
                case "getColumnValues":
                    await HandleGetColumnValuesAsync(msg);
                    break;
                case "exportCsv":
                    await HandleExportCsvAsync(msg);
                    break;
                case "jsError":
                    LogDebug($"JS error: {msg["message"]?.GetValue<string>()} ({msg["source"]?.GetValue<string>()})");
                    break;
            }
        }
        catch (Exception ex)
        {
            await PostErrorAsync(action ?? "unknown", ex.Message);
        }
    }

    private async Task HandleQueryProcessesAsync(JsonNode msg)
    {
        try
        {
            var query = new ProcessEventQuery(
                Include: ReadFilter(msg, "include", "includeMode"),
                Exclude: ReadFilter(msg, "exclude", "excludeMode"),
                Take: msg["take"]?.GetValue<int>() ?? 100);

            var rows = await _processQueryService.QueryAsync(query);
            await PostAsync("onProcessRows", rows);
        }
        catch (ArgumentException ex)
        {
            await PostErrorAsync("queryProcesses", ex.Message);
        }
    }

    private async Task HandleQueryFocusAsync(JsonNode msg)
    {
        try
        {
            var query = new FocusEventQuery(
                Include: ReadFilter(msg, "include", "includeMode"),
                Exclude: ReadFilter(msg, "exclude", "excludeMode"),
                Take: msg["take"]?.GetValue<int>() ?? 100);

            var rows = await _focusQueryService.QueryAsync(query);
            await PostAsync("onFocusRows", rows);
        }
        catch (ArgumentException ex)
        {
            await PostErrorAsync("queryFocus", ex.Message);
        }
    }

    private static TextFilter? ReadFilter(JsonNode msg, string textField, string modeField)
    {
        var text = msg[textField]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(text)) return null;

        var modeText = msg[modeField]?.GetValue<string>();
        var mode = modeText is not null && Enum.TryParse<TextFilterMode>(modeText, out var parsed)
            ? parsed
            : TextFilterMode.PartialText;
        return new TextFilter(text, mode);
    }

    private async Task HandleGetColumnValuesAsync(JsonNode msg)
    {
        var entity = msg["entity"]?.GetValue<string>();
        var column = msg["column"]?.GetValue<string>();
        if (entity is null || column is null) return;

        var values = entity == "focus"
            ? await GetFocusColumnValuesAsync(column)
            : await GetProcessColumnValuesAsync(column);

        await PostAsync("onColumnValues", new { entity, column, values });
    }

    private async Task<object?[]> GetProcessColumnValuesAsync(string column) => column switch
    {
        "Name" => (await _processQueryService.GetDistinctValuesAsync(e => e.Name)).Cast<object?>().ToArray(),
        "Title" => (await _processQueryService.GetDistinctValuesAsync(e => e.Title)).Cast<object?>().ToArray(),
        "ProcessId" => (await _processQueryService.GetDistinctValuesAsync(e => e.ProcessId)).Cast<object?>().ToArray(),
        "ParentProcessName" => (await _processQueryService.GetDistinctValuesAsync(e => e.ParentProcessName)).Cast<object?>().ToArray(),
        "Path" => (await _processQueryService.GetDistinctValuesAsync(e => e.Path)).Cast<object?>().ToArray(),
        "DurationMs" => (await _processQueryService.GetDistinctValuesAsync(e => e.DurationMs)).Cast<object?>().ToArray(),
        "PeakWorkingSetBytes" => (await _processQueryService.GetDistinctValuesAsync(e => e.PeakWorkingSetBytes)).Cast<object?>().ToArray(),
        "FinalWorkingSetBytes" => (await _processQueryService.GetDistinctValuesAsync(e => e.FinalWorkingSetBytes)).Cast<object?>().ToArray(),
        "SampleCount" => (await _processQueryService.GetDistinctValuesAsync(e => e.SampleCount)).Cast<object?>().ToArray(),
        "Source" => (await _processQueryService.GetDistinctValuesAsync(e => e.Source)).Select(s => (object?)s.ToString()).ToArray(),
        _ => [],
    };

    private async Task<object?[]> GetFocusColumnValuesAsync(string column) => column switch
    {
        "ProcessName" => (await _focusQueryService.GetDistinctValuesAsync(e => e.ProcessName)).Cast<object?>().ToArray(),
        "ProcessId" => (await _focusQueryService.GetDistinctValuesAsync(e => e.ProcessId)).Cast<object?>().ToArray(),
        "WindowTitle" => (await _focusQueryService.GetDistinctValuesAsync(e => e.WindowTitle)).Cast<object?>().ToArray(),
        _ => [],
    };

    // A Save-dialog path is exact and the dialog already confirmed any overwrite.
    private static readonly ArtifactOptions ChosenFile = new() { Existing = ExistingArtifact.Overwrite, SanitizeName = false };

    private async Task HandleExportCsvAsync(JsonNode msg)
    {
        var entity = msg["entity"]?.GetValue<string>() ?? "process";
        var csv = msg["csv"]?.GetValue<string>() ?? "";
        var suggestedName = msg["suggestedName"]?.GetValue<string>() ?? "processlogger-export.csv";

        var dialog = new SaveFileDialog
        {
            Title = "Export to CSV",
            FileName = suggestedName,
            Filter = "CSV (*.csv)|*.csv",
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            await ArtifactWriter.WriteTextAsync(
                Path.GetDirectoryName(dialog.FileName)!, Path.GetFileName(dialog.FileName), csv, ChosenFile);
            await PostAsync("onExportSaved", new { entity, fileName = Path.GetFileName(dialog.FileName) });
        }
        catch (IOException ex)
        {
            await PostErrorAsync("exportCsv", ex.Message, entity);
        }
    }

    private async Task PostAsync(string jsFunction, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await TryExecuteScriptAsync($"window.processLogger.{jsFunction}({json})");
    }

    private async Task PostErrorAsync(string action, string message, string? entity = null)
    {
        var json = JsonSerializer.Serialize(new { action, message, entity }, JsonOptions);
        await TryExecuteScriptAsync($"window.processLogger.onError({json})");
    }

    /// <summary>A hung WebView2 renderer can make ExecuteScriptAsync block forever with zero
    /// exception - guard every call so a stuck script can never silently freeze a query.</summary>
    private async Task TryExecuteScriptAsync(string script, int timeoutMs = 8000)
    {
        try
        {
            var scriptTask = Browser.CoreWebView2.ExecuteScriptAsync(script);
            var winner = await Task.WhenAny(scriptTask, Task.Delay(timeoutMs));
            if (winner == scriptTask) await scriptTask;
        }
        catch (Exception ex)
        {
            LogDebug($"WebView2 script call failed: {ex.Message}");
        }
    }

    private static readonly string DebugLogPath = Path.Combine(Path.GetTempPath(), "processlogger-debug.log");

    private static void LogDebug(string line)
    {
        try
        {
            File.AppendAllText(DebugLogPath, $"[{DateTime.Now:HH:mm:ss}] {line}\n");
        }
        catch (IOException)
        {
        }
    }
}
