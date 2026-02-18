namespace Logsmith.Sinks;

public class DebugSink : ILogSink
{
    public bool IsEnabled(LogLevel level) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // Stub — full implementation in Plan 3.
        throw new NotImplementedException();
    }

    public void Dispose() { }
}
