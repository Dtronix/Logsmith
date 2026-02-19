using Logsmith.Internal;

namespace Logsmith;

public static class LogManager
{
    private static volatile LogConfig? _config;
    private static int _initialized;

    public static void Initialize(Action<LogConfigBuilder> configure)
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
            throw new InvalidOperationException("LogManager has already been initialized. Use Reconfigure to change configuration.");

        var builder = new LogConfigBuilder();
        configure(builder);
        _config = builder.Build();
    }

    public static void Reconfigure(Action<LogConfigBuilder> configure)
    {
        var old = _config;
        var builder = new LogConfigBuilder();
        configure(builder);
        _config = builder.Build();
        old?.DisposeMonitors();
    }

    public static bool IsEnabled(LogLevel level)
    {
        var config = _config;
        if (config is null) return false;
        return level >= config.MinimumLevel;
    }

    public static bool IsEnabled(LogLevel level, string category)
    {
        var config = _config;
        if (config is null) return false;

        if (config.CategoryOverrides.TryGetValue(category, out var categoryLevel))
            return level >= categoryLevel;

        return level >= config.MinimumLevel;
    }

    public static void Dispatch<TState>(
        in LogEntry entry,
        ReadOnlySpan<byte> utf8Message,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct
    {
        var config = _config;
        if (config is null) return;

        var sinkSet = config.Sinks;

        var errorHandler = config.ErrorHandler;

        // Append scope properties to text message if scopes are active
        Span<byte> scopeBuffer = stackalloc byte[512];
        var scopeWriter = new Utf8LogWriter(scopeBuffer);
        scopeWriter.Write(utf8Message);
        var scope = LogScope.Current;
        if (scope is not null)
        {
            LogScope.WriteScopeToUtf8(ref scopeWriter);
        }
        var messageToWrite = scopeWriter.GetWritten();

        var textSinks = sinkSet.TextSinks;
        for (int i = 0; i < textSinks.Length; i++)
        {
            if (textSinks[i].IsEnabled(entry.Level))
            {
                try
                {
                    textSinks[i].Write(in entry, messageToWrite);
                }
                catch (Exception ex)
                {
                    errorHandler?.Invoke(ex);
                }
            }
        }

        var structuredSinks = sinkSet.StructuredSinks;
        for (int i = 0; i < structuredSinks.Length; i++)
        {
            if (structuredSinks[i].IsEnabled(entry.Level))
            {
                try
                {
                    structuredSinks[i].WriteStructured(in entry, state, propertyWriter);
                }
                catch (Exception ex)
                {
                    errorHandler?.Invoke(ex);
                }
            }
        }
    }

    internal static void SetMinimumLevel(LogLevel level)
    {
        var current = _config;
        if (current is null) return;

        var newConfig = new LogConfig(level, current.CategoryOverrides.ToDictionary(), current.Sinks, current.ErrorHandler, current.Monitors);
        _config = newConfig;
    }

    internal static void SetCategoryOverrides(Dictionary<string, LogLevel> overrides)
    {
        var current = _config;
        if (current is null) return;

        var newConfig = new LogConfig(current.MinimumLevel, overrides, current.Sinks, current.ErrorHandler, current.Monitors);
        _config = newConfig;
    }

    // For testing: reset state so Initialize can be called again.
    internal static void Reset()
    {
        var old = _config;
        _config = null;
        Interlocked.Exchange(ref _initialized, 0);
        old?.DisposeMonitors();
    }
}
