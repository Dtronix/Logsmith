using System.Buffers;
using Logsmith.Formatting;

namespace Logsmith.Sinks;

public class StreamSink : BufferedLogSink
{
    private readonly Stream _stream;
    private readonly ILogFormatter _formatter;
    private readonly bool _leaveOpen;

    public StreamSink(Stream stream, LogLevel minimumLevel = LogLevel.Trace,
                      ILogFormatter? formatter = null, bool leaveOpen = false, int capacity = 1024)
        : base(minimumLevel, capacity)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _formatter = formatter ?? new DefaultLogFormatter(includeDate: true);
        _leaveOpen = leaveOpen;
    }

    protected override async Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct)
    {
        var logEntry = new LogEntry(
            entry.Level, entry.EventId, entry.TimestampTicks, entry.Category,
            entry.Exception, entry.CallerFile, entry.CallerLine, entry.CallerMember,
            entry.ThreadId, entry.ThreadName);

        var buffer = new ArrayBufferWriter<byte>(256);
        _formatter.FormatPrefix(in logEntry, buffer);
        buffer.Write(entry.Utf8Message);
        _formatter.FormatSuffix(in logEntry, buffer);

        await _stream.WriteAsync(buffer.WrittenMemory, ct);
        await _stream.FlushAsync(ct);
    }

    protected override async ValueTask OnDisposeAsync()
    {
        await _stream.FlushAsync();
        if (!_leaveOpen)
            await _stream.DisposeAsync();
    }
}
