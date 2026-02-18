namespace Logsmith;

public readonly struct LogEntry
{
    public readonly LogLevel Level;
    public readonly int EventId;
    public readonly long TimestampTicks;
    public readonly string Category;
    public readonly Exception? Exception;
    public readonly string? CallerFile;
    public readonly int CallerLine;
    public readonly string? CallerMember;

    public LogEntry(
        LogLevel level,
        int eventId,
        long timestampTicks,
        string category,
        Exception? exception = null,
        string? callerFile = null,
        int callerLine = 0,
        string? callerMember = null)
    {
        Level = level;
        EventId = eventId;
        TimestampTicks = timestampTicks;
        Category = category;
        Exception = exception;
        CallerFile = callerFile;
        CallerLine = callerLine;
        CallerMember = callerMember;
    }
}
