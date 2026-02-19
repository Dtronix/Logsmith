using System.Buffers;
using Logsmith.Formatting;
using Logsmith.Internal;

namespace Logsmith.Sinks;

public class ConsoleSink : TextLogSink
{
    private readonly bool _colored;
    private readonly Stream _stdout;
    private readonly ILogFormatter _formatter;

    private static ReadOnlySpan<byte> ResetCode => "\x1b[0m"u8;

    public ConsoleSink(bool colored = true, LogLevel minimumLevel = LogLevel.Trace, ILogFormatter? formatter = null)
        : base(minimumLevel)
    {
        _colored = colored && !Console.IsOutputRedirected;
        _stdout = Console.OpenStandardOutput();
        _formatter = formatter ?? new DefaultLogFormatter(includeDate: false);
    }

    protected override void WriteMessage(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var buf = ThreadBuffer.Get();

        if (_colored)
        {
            var colorCode = GetAnsiColor(entry.Level);
            buf.Write(colorCode);
        }

        _formatter.FormatPrefix(in entry, buf);
        buf.Write(utf8Message);
        _formatter.FormatSuffix(in entry, buf);

        if (_colored)
            buf.Write(ResetCode);

        _stdout.Write(buf.WrittenSpan);
        _stdout.Flush();
    }

    private static ReadOnlySpan<byte> GetAnsiColor(LogLevel level) => level switch
    {
        LogLevel.Trace => "\x1b[90m"u8,     // Gray
        LogLevel.Debug => "\x1b[36m"u8,     // Cyan
        LogLevel.Information => "\x1b[32m"u8, // Green
        LogLevel.Warning => "\x1b[33m"u8,   // Yellow
        LogLevel.Error => "\x1b[31m"u8,     // Red
        LogLevel.Critical => "\x1b[1;31m"u8, // Bold Red
        _ => ""u8
    };

    public override void Dispose()
    {
        _stdout.Dispose();
        base.Dispose();
    }
}
