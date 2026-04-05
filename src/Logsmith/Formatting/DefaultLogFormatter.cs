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

    public void FormatPrefix(in DispatchInfo info, IBufferWriter<byte> output)
    {
        var timestamp = new DateTime(info.TimestampTicks, DateTimeKind.Utc);

        // Format: [HH:mm:ss.fff LVL Category|Path #Tag] or [yyyy-MM-dd HH:mm:ss.fff LVL Category|Path #Tag]
        // Compute required size: fixed prefix + category + path + tag
        var catBytes = _categoryUtf8Cache.GetOrAdd(info.Category ?? "", static c => Encoding.UTF8.GetBytes(c));
        int needed = 32 // [yyyy-MM-dd HH:mm:ss.fff LVL ] + brackets + space margin
            + catBytes.Length
            + (info.Utf8Path.Length > 0 ? 1 + info.Utf8Path.Length : 0) // |path
            + (info.Tag is not null ? 2 + Encoding.UTF8.GetByteCount(info.Tag) : 0) // _#tag
            + 2; // ] and trailing space
        var span = output.GetSpan(needed);
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
        var levelTag = GetLevelTag(info.Level);
        levelTag.CopyTo(span[pos..]);
        pos += levelTag.Length;

        span[pos++] = (byte)' ';

        // Category
        catBytes.CopyTo(span[pos..]);
        pos += catBytes.Length;

        // Path (if present): |Segment1|Segment2
        if (info.Utf8Path.Length > 0)
        {
            span[pos++] = (byte)'|';
            info.Utf8Path.CopyTo(span[pos..]);
            pos += info.Utf8Path.Length;
        }

        // Tag (if present): #TagName
        if (info.Tag is not null)
        {
            span[pos++] = (byte)' ';
            span[pos++] = (byte)'#';
            var tagByteCount = Encoding.UTF8.GetBytes(info.Tag, span[pos..]);
            pos += tagByteCount;
        }

        span[pos++] = (byte)']';
        span[pos++] = (byte)' ';

        output.Advance(pos);
    }

    public void FormatSuffix(in DispatchInfo info, IBufferWriter<byte> output)
    {
        // Newline
        var span = output.GetSpan(1);
        span[0] = (byte)'\n';
        output.Advance(1);

        // Exception on next line if present
        if (info.Exception is not null)
        {
            WriteExceptionUtf8(info.Exception, output);
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
