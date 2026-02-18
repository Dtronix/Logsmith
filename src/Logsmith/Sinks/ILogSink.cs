namespace Logsmith;

public interface ILogSink : IDisposable
{
    bool IsEnabled(LogLevel level);
    void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
}
