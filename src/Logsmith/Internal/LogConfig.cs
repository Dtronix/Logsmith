using System.Collections.Frozen;

namespace Logsmith.Internal;

internal sealed class LogConfig
{
    internal readonly LogLevel MinimumLevel;
    internal readonly FrozenDictionary<string, LogLevel> CategoryOverrides;
    internal readonly SinkSet Sinks;
    internal readonly Action<Exception>? ErrorHandler;
    internal readonly IDisposable[]? Monitors;

    internal LogConfig(
        LogLevel minimumLevel,
        Dictionary<string, LogLevel> categoryOverrides,
        SinkSet sinks,
        Action<Exception>? errorHandler = null,
        IDisposable[]? monitors = null)
    {
        MinimumLevel = minimumLevel;
        CategoryOverrides = categoryOverrides.ToFrozenDictionary();
        Sinks = sinks;
        ErrorHandler = errorHandler;
        Monitors = monitors;
    }

    internal void DisposeMonitors()
    {
        if (Monitors is null) return;
        foreach (var monitor in Monitors)
        {
            try { monitor.Dispose(); } catch { }
        }
    }
}
