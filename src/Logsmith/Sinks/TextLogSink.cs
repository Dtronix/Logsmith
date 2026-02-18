namespace Logsmith.Sinks;

public abstract class TextLogSink : ILogSink
{
    protected LogLevel MinimumLevel { get; }

    protected TextLogSink(LogLevel minimumLevel = LogLevel.Trace)
    {
        MinimumLevel = minimumLevel;
    }

    public virtual bool IsEnabled(LogLevel level) => level >= MinimumLevel;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        WriteMessage(in entry, utf8Message);
    }

    protected abstract void WriteMessage(in LogEntry entry, ReadOnlySpan<byte> utf8Message);

    public virtual void Dispose() { }
}
