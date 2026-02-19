using Logsmith.Formatting;
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

    public void SetMinimumLevel<T>(LogLevel level)
    {
        const string fieldName = "CategoryName";
        var field = typeof(T).GetField(fieldName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (field is not null && field.FieldType == typeof(string))
        {
            var category = (string)field.GetValue(null)!;
            _categoryOverrides[category] = level;
        }
        else
        {
            _categoryOverrides[typeof(T).Name] = level;
        }
    }

    public void AddSink(ILogSink sink)
    {
        _sinks.Add(sink);
    }

    public void AddConsoleSink(bool colored = true, ILogFormatter? formatter = null)
    {
        AddSink(new Sinks.ConsoleSink(colored, formatter: formatter));
    }

    public void AddFileSink(string path, ILogFormatter? formatter = null, bool shared = false,
                            Sinks.RollingInterval rollingInterval = Sinks.RollingInterval.None,
                            long maxFileSizeBytes = 10 * 1024 * 1024)
    {
        AddSink(new Sinks.FileSink(path, formatter: formatter, shared: shared,
            rollingInterval: rollingInterval, maxFileSizeBytes: maxFileSizeBytes));
    }

    public void AddDebugSink(ILogFormatter? formatter = null)
    {
        AddSink(new Sinks.DebugSink(formatter: formatter));
    }

    public void AddStreamSink(Stream stream, bool leaveOpen = false, ILogFormatter? formatter = null)
    {
        AddSink(new Sinks.StreamSink(stream, formatter: formatter, leaveOpen: leaveOpen));
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
