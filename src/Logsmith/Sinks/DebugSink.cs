using System.Diagnostics;
using System.Text;

namespace Logsmith.Sinks;

public class DebugSink : ILogSink
{
    private readonly LogLevel _minimumLevel;

    public DebugSink(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    public bool IsEnabled(LogLevel level) => Debugger.IsAttached && level >= _minimumLevel;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var message = Encoding.UTF8.GetString(utf8Message);
        Debug.WriteLine(message);
    }

    public void Dispose() { }
}
