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

    public void Write(in DispatchInfo info)
    {
        var buffer = ThreadBuffer.Get();
        _formatter.FormatPrefix(in info, buffer);
        buffer.Write(info.Utf8Message);
        _formatter.FormatSuffix(in info, buffer);

        var message = Encoding.UTF8.GetString(buffer.WrittenSpan);
        Debug.WriteLine(message);
    }

    public void Dispose() { }
}
