namespace Logsmith.Internal;

internal sealed class LogConfig
{
    internal readonly LogLevel MinimumLevel;
    internal readonly Dictionary<string, LogLevel> CategoryOverrides;
    internal readonly SinkSet Sinks;
    internal readonly Action<Exception>? ErrorHandler;

    internal LogConfig(
        LogLevel minimumLevel,
        Dictionary<string, LogLevel> categoryOverrides,
        SinkSet sinks,
        Action<Exception>? errorHandler = null)
    {
        MinimumLevel = minimumLevel;
        CategoryOverrides = categoryOverrides;
        Sinks = sinks;
        ErrorHandler = errorHandler;
    }
}
