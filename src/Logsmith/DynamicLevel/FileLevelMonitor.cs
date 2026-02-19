using System.Text.Json;

namespace Logsmith.DynamicLevel;

internal sealed class FileLevelMonitor : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly string _filePath;
    private Timer? _debounceTimer;

    internal FileLevelMonitor(string filePath)
    {
        _filePath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(_filePath)!;
        var fileName = Path.GetFileName(_filePath);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnFileChanged;

        // Initial read
        TryApplyConfig();
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce: reset timer on each change, apply after 500ms of quiet
        _debounceTimer?.Dispose();
        _debounceTimer = new Timer(_ => TryApplyConfig(), null, 500, Timeout.Infinite);
    }

    private void TryApplyConfig()
    {
        try
        {
            if (!File.Exists(_filePath)) return;

            var json = File.ReadAllText(_filePath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("MinimumLevel", out var levelProp))
            {
                var levelStr = levelProp.GetString();
                if (levelStr is not null && Enum.TryParse<LogLevel>(levelStr, ignoreCase: true, out var level))
                {
                    LogManager.SetMinimumLevel(level);
                }
            }

            if (root.TryGetProperty("CategoryOverrides", out var overridesProp) &&
                overridesProp.ValueKind == JsonValueKind.Object)
            {
                var overrides = new Dictionary<string, LogLevel>();
                foreach (var prop in overridesProp.EnumerateObject())
                {
                    var valStr = prop.Value.GetString();
                    if (valStr is not null && Enum.TryParse<LogLevel>(valStr, ignoreCase: true, out var catLevel))
                    {
                        overrides[prop.Name] = catLevel;
                    }
                }
                LogManager.SetCategoryOverrides(overrides);
            }
        }
        catch
        {
            // Silently ignore parse errors — file may be partially written
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _debounceTimer?.Dispose();
    }
}
