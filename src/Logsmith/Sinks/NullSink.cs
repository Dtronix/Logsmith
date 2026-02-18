namespace Logsmith.Sinks;

public class NullSink : ILogSink
{
    public bool IsEnabled(LogLevel level) => false;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message) { }

    public void Dispose() { }
}
