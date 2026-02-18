namespace Logsmith.Sinks;

public class ConsoleSink : ILogSink
{
    private readonly bool _colored;

    public ConsoleSink(bool colored = true)
    {
        _colored = colored;
    }

    public bool IsEnabled(LogLevel level) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // Stub — full implementation in Plan 3.
        throw new NotImplementedException();
    }

    public void Dispose() { }
}
