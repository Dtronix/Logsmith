using System.Text;

namespace Logsmith.Sinks;

public class ConsoleSink : TextLogSink
{
    private readonly bool _colored;
    private readonly Stream _stdout;

    private static ReadOnlySpan<byte> ResetCode => "\x1b[0m"u8;

    public ConsoleSink(bool colored = true, LogLevel minimumLevel = LogLevel.Trace)
        : base(minimumLevel)
    {
        _colored = colored;
        _stdout = Console.OpenStandardOutput();
    }

    protected override void WriteMessage(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var timestamp = new DateTime(entry.TimestampTicks, DateTimeKind.Utc);
        var levelTag = GetLevelTag(entry.Level);
        var prefix = $"[{timestamp:HH:mm:ss.fff} {levelTag} {entry.Category}] ";

        Span<byte> prefixBuffer = stackalloc byte[256];
        int prefixLen = Encoding.UTF8.GetBytes(prefix, prefixBuffer);

        if (_colored)
        {
            var colorCode = GetAnsiColor(entry.Level);
            _stdout.Write(colorCode);
            _stdout.Write(prefixBuffer[..prefixLen]);
            _stdout.Write(utf8Message);
            _stdout.Write(ResetCode);
            _stdout.Write("\n"u8);
        }
        else
        {
            _stdout.Write(prefixBuffer[..prefixLen]);
            _stdout.Write(utf8Message);
            _stdout.Write("\n"u8);
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

    private static string GetLevelTag(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???"
    };

    public override void Dispose()
    {
        _stdout.Dispose();
        base.Dispose();
    }
}
