using System.Buffers;
using System.Collections.Concurrent;
using System.Text;

namespace Logsmith.Formatting;

public sealed class DefaultLogFormatter : ILogFormatter
{
    private readonly bool _includeDate;
    private readonly ConcurrentDictionary<string, byte[]> _categoryUtf8Cache = new();

    private static ReadOnlySpan<byte> TrcTag => "TRC"u8;
    private static ReadOnlySpan<byte> DbgTag => "DBG"u8;
    private static ReadOnlySpan<byte> InfTag => "INF"u8;
    private static ReadOnlySpan<byte> WrnTag => "WRN"u8;
    private static ReadOnlySpan<byte> ErrTag => "ERR"u8;
    private static ReadOnlySpan<byte> CrtTag => "CRT"u8;
    private static ReadOnlySpan<byte> UnkTag => "???"u8;

    public DefaultLogFormatter(bool includeDate = false)
    {
        _includeDate = includeDate;
    }

    public void FormatPrefix(in LogEntry entry, IBufferWriter<byte> output)
    {
        var timestamp = new DateTime(entry.TimestampTicks, DateTimeKind.Utc);

        // Format: [HH:mm:ss.fff LVL Category] or [yyyy-MM-dd HH:mm:ss.fff LVL Category]
        // Max: [yyyy-MM-dd HH:mm:ss.fff CRT LongCategory] + space ≈ 80 bytes typical
        var span = output.GetSpan(256);
        int pos = 0;

        span[pos++] = (byte)'[';

        if (_includeDate)
        {
            // yyyy-MM-dd
            timestamp.TryFormat(span[pos..], out int dateWritten, "yyyy-MM-dd ", null);
            pos += dateWritten;
        }

        // HH:mm:ss.fff
        timestamp.TryFormat(span[pos..], out int timeWritten, "HH:mm:ss.fff", null);
        pos += timeWritten;

        span[pos++] = (byte)' ';

        // Level tag
        var levelTag = GetLevelTag(entry.Level);
        levelTag.CopyTo(span[pos..]);
        pos += levelTag.Length;

        span[pos++] = (byte)' ';

        // Category
        var catBytes = _categoryUtf8Cache.GetOrAdd(entry.Category, static c => Encoding.UTF8.GetBytes(c));
        catBytes.CopyTo(span[pos..]);
        pos += catBytes.Length;

        span[pos++] = (byte)']';
        span[pos++] = (byte)' ';

        output.Advance(pos);
    }

    public void FormatSuffix(in LogEntry entry, IBufferWriter<byte> output)
    {
        // Newline
        var span = output.GetSpan(1);
        span[0] = (byte)'\n';
        output.Advance(1);

        // Exception on next line if present
        if (entry.Exception is not null)
        {
            WriteExceptionUtf8(entry.Exception, output);
        }
    }

    private static void WriteExceptionUtf8(Exception ex, IBufferWriter<byte> output)
    {
        var exStr = ex.ToString();
        var chars = exStr.AsSpan();
        var encoder = Encoding.UTF8.GetEncoder();

        const int charChunkSize = 1024;
        while (chars.Length > 0)
        {
            var chunk = chars.Length > charChunkSize ? chars[..charChunkSize] : chars;
            var maxBytes = Encoding.UTF8.GetMaxByteCount(chunk.Length);
            var span = output.GetSpan(maxBytes);
            encoder.Convert(chunk, span, chunk.Length == chars.Length, out int charsUsed, out int bytesWritten, out _);
            output.Advance(bytesWritten);
            chars = chars[charsUsed..];
        }

        var nl = output.GetSpan(1);
        nl[0] = (byte)'\n';
        output.Advance(1);
    }

    private static ReadOnlySpan<byte> GetLevelTag(LogLevel level) => level switch
    {
        LogLevel.Trace => TrcTag,
        LogLevel.Debug => DbgTag,
        LogLevel.Information => InfTag,
        LogLevel.Warning => WrnTag,
        LogLevel.Error => ErrTag,
        LogLevel.Critical => CrtTag,
        _ => UnkTag
    };
}
