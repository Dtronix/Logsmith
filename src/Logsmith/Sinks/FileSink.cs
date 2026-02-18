namespace Logsmith.Sinks;

public class FileSink : ILogSink, IAsyncDisposable
{
    private readonly string _path;

    public FileSink(string path)
    {
        _path = path;
    }

    public bool IsEnabled(LogLevel level) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // Stub — full implementation in Plan 3.
        throw new NotImplementedException();
    }

    public void Dispose() { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
