namespace Logsmith.Sinks;

public abstract class TextLogSink : ILogSink
{
    protected LogLevel MinimumLevel { get; }

    protected TextLogSink(LogLevel minimumLevel = LogLevel.Trace)
    {
        MinimumLevel = minimumLevel;
    }

    public virtual bool IsEnabled(LogLevel level) => level >= MinimumLevel;

    public void Write(in DispatchInfo info)
    {
        WriteMessage(in info);
    }

    protected abstract void WriteMessage(in DispatchInfo info);

    public virtual void Dispose() { }
}
