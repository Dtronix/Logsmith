using Logsmith.Internal;

namespace Logsmith;

public sealed class LogConfigBuilder
{
    private readonly List<ILogSink> _sinks = new();
    private readonly Dictionary<string, LogLevel> _categoryOverrides = new();

    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public Action<Exception>? InternalErrorHandler { get; set; }

    public void SetMinimumLevel(string category, LogLevel level)
    {
        _categoryOverrides[category] = level;
    }

    public void AddSink(ILogSink sink)
    {
        _sinks.Add(sink);
    }

    // Convenience methods — concrete sink types are defined in Plan 3 (Sinks/).
    // These will be updated to instantiate ConsoleSink, FileSink, DebugSink
    // once those types are implemented in the same assembly.

    public void AddConsoleSink(bool colored = true)
    {
        AddSink(new Sinks.ConsoleSink(colored));
    }

    public void AddFileSink(string path)
    {
        AddSink(new Sinks.FileSink(path));
    }

    public void AddDebugSink()
    {
        AddSink(new Sinks.DebugSink());
    }

    public void ClearSinks()
    {
        _sinks.Clear();
    }

    internal LogConfig Build()
    {
        var sinkSet = SinkSet.Classify(_sinks);
        return new LogConfig(MinimumLevel, new Dictionary<string, LogLevel>(_categoryOverrides), sinkSet, InternalErrorHandler);
    }
}
