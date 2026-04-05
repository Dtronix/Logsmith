namespace Logsmith;

/// <summary>
/// Carries all log dispatch parameters through the dispatch path.
/// Replaces LogEntry as the single data carrier for all logging APIs.
/// </summary>
public ref struct DispatchInfo
{
    public LogLevel Level;
    public int EventId;
    public long TimestampTicks;
    public string Category;
    public ReadOnlySpan<byte> Utf8Message;
    public ReadOnlySpan<byte> Utf8Json;
    public ReadOnlySpan<byte> Utf8Path;
    public Exception? Exception;
    public string? Tag;
    public string? CallerFile;
    public int CallerLine;
    public string? CallerMember;
    public int ThreadId;
    public string? ThreadName;
}
