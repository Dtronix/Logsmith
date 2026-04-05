using System.Text;

namespace Logsmith.Sinks;

public class RecordingSink : ILogSink
{
    public record CapturedEntry(
        LogLevel Level,
        int EventId,
        long TimestampTicks,
        string Category,
        Exception? Exception,
        string? Tag,
        string? CallerFile,
        int CallerLine,
        string? CallerMember,
        int ThreadId,
        string? ThreadName,
        string Message,
        string? JsonMessage,
        string? Path);

    private readonly LogLevel _minimumLevel;

    public List<CapturedEntry> Entries { get; } = new();

    public RecordingSink(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    public bool IsEnabled(LogLevel level) => level >= _minimumLevel;

    public void Write(in DispatchInfo info)
    {
        var message = Encoding.UTF8.GetString(info.Utf8Message);
        var json = info.Utf8Json.Length > 0 ? Encoding.UTF8.GetString(info.Utf8Json) : null;
        var path = info.Utf8Path.Length > 0 ? Encoding.UTF8.GetString(info.Utf8Path) : null;
        Entries.Add(new CapturedEntry(
            info.Level,
            info.EventId,
            info.TimestampTicks,
            info.Category,
            info.Exception,
            info.Tag,
            info.CallerFile,
            info.CallerLine,
            info.CallerMember,
            info.ThreadId,
            info.ThreadName,
            message,
            json,
            path));
    }

    public void Clear() => Entries.Clear();

    public void Dispose() { }
}
