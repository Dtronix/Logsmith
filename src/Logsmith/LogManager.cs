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
        var builder = new LogConfigBuilder();
        configure(builder);
        _config = builder.Build();
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

        var textSinks = sinkSet.TextSinks;
        for (int i = 0; i < textSinks.Length; i++)
        {
            if (textSinks[i].IsEnabled(entry.Level))
            {
                try
                {
                    textSinks[i].Write(in entry, utf8Message);
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

    // For testing: reset state so Initialize can be called again.
    internal static void Reset()
    {
        _config = null;
        Interlocked.Exchange(ref _initialized, 0);
    }
}
