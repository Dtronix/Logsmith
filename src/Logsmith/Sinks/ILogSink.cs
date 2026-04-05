namespace Logsmith;

public interface ILogSink : IDisposable
{
    bool IsEnabled(LogLevel level);
    void Write(in DispatchInfo info);
}
