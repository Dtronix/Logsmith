namespace Logsmith.Sinks;

public class NullSink : ILogSink
{
    public bool IsEnabled(LogLevel level) => false;

    public void Write(in DispatchInfo info) { }

    public void Dispose() { }
}
