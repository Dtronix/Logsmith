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
        string? CallerFile,
        int CallerLine,
        string? CallerMember,
        string Message);

    private readonly LogLevel _minimumLevel;

    public List<CapturedEntry> Entries { get; } = new();

    public RecordingSink(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    public bool IsEnabled(LogLevel level) => level >= _minimumLevel;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var message = Encoding.UTF8.GetString(utf8Message);
        Entries.Add(new CapturedEntry(
            entry.Level,
            entry.EventId,
            entry.TimestampTicks,
            entry.Category,
            entry.Exception,
            entry.CallerFile,
            entry.CallerLine,
            entry.CallerMember,
            message));
    }

    public void Clear() => Entries.Clear();

    public void Dispose() { }
}
