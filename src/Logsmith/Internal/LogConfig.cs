using System.Collections.Frozen;

namespace Logsmith.Internal;

internal sealed class LogConfig
{
    internal readonly LogLevel MinimumLevel;
    internal readonly FrozenDictionary<string, LogLevel> CategoryOverrides;
    internal readonly SinkSet Sinks;
    internal readonly Action<Exception>? ErrorHandler;
    internal readonly IDisposable[]? Monitors;
    internal readonly bool CaptureUnhandledExceptions;
    internal readonly bool ObserveTaskExceptions;

    internal LogConfig(
        LogLevel minimumLevel,
        Dictionary<string, LogLevel> categoryOverrides,
        SinkSet sinks,
        Action<Exception>? errorHandler = null,
        IDisposable[]? monitors = null,
        bool captureUnhandledExceptions = false,
        bool observeTaskExceptions = false)
    {
        MinimumLevel = minimumLevel;
        CategoryOverrides = categoryOverrides.ToFrozenDictionary();
        Sinks = sinks;
        ErrorHandler = errorHandler;
        Monitors = monitors;
        CaptureUnhandledExceptions = captureUnhandledExceptions;
        ObserveTaskExceptions = observeTaskExceptions;
    }

    internal void DisposeMonitors()
    {
        if (Monitors is null) return;
        foreach (var monitor in Monitors)
        {
            try { monitor.Dispose(); } catch { }
        }
    }

    internal async ValueTask DisposeAllAsync()
    {
        DisposeMonitors();
        await Sinks.DisposeSinksAsync().ConfigureAwait(false);
    }
}
