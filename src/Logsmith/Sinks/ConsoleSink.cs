using System.Buffers;
using Logsmith.Formatting;

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
        _colored = colored;
        _stdout = Console.OpenStandardOutput();
        _formatter = formatter ?? new DefaultLogFormatter(includeDate: false);
    }

    protected override void WriteMessage(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        _formatter.FormatPrefix(in entry, buffer);
        var prefixBytes = buffer.WrittenSpan;

        buffer.ResetWrittenCount();
        _formatter.FormatSuffix(in entry, buffer);
        var suffixBytes = buffer.WrittenSpan;

        if (_colored)
        {
            var colorCode = GetAnsiColor(entry.Level);
            _stdout.Write(colorCode);
            _stdout.Write(prefixBytes);
            _stdout.Write(utf8Message);
            _stdout.Write(suffixBytes);
            _stdout.Write(ResetCode);
        }
        else
        {
            _stdout.Write(prefixBytes);
            _stdout.Write(utf8Message);
            _stdout.Write(suffixBytes);
        }

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
