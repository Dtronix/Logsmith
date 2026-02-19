using System.Buffers;
using System.Diagnostics;
using System.Text;
using Logsmith.Formatting;
using Logsmith.Internal;

namespace Logsmith.Sinks;

public class DebugSink : ILogSink
{
    private readonly LogLevel _minimumLevel;
    private readonly ILogFormatter _formatter;

    public DebugSink(LogLevel minimumLevel = LogLevel.Trace, ILogFormatter? formatter = null)
    {
        _minimumLevel = minimumLevel;
        _formatter = formatter ?? new DefaultLogFormatter(includeDate: false);
    }

    public bool IsEnabled(LogLevel level) => Debugger.IsAttached && level >= _minimumLevel;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var buffer = ThreadBuffer.Get();
        _formatter.FormatPrefix(in entry, buffer);
        buffer.Write(utf8Message);
        _formatter.FormatSuffix(in entry, buffer);

        var message = Encoding.UTF8.GetString(buffer.WrittenSpan);
        Debug.WriteLine(message);
    }

    public void Dispose() { }
}
