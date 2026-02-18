namespace Logsmith.Internal;

internal sealed class LogConfig
{
    internal readonly LogLevel MinimumLevel;
    internal readonly Dictionary<string, LogLevel> CategoryOverrides;
    internal readonly SinkSet Sinks;

    internal LogConfig(
        LogLevel minimumLevel,
        Dictionary<string, LogLevel> categoryOverrides,
        SinkSet sinks)
    {
        MinimumLevel = minimumLevel;
        CategoryOverrides = categoryOverrides;
        Sinks = sinks;
    }
}
