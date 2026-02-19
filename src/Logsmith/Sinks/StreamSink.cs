using System.Buffers;
using Logsmith.Formatting;
using Logsmith.Internal;

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
        var logEntry = entry.Entry;
        var utf8Message = entry.Utf8MessageBuffer.AsSpan(0, entry.Utf8MessageLength);

        var buf = ThreadBuffer.Get();
        _formatter.FormatPrefix(in logEntry, buf);
        buf.Write(utf8Message);
        _formatter.FormatSuffix(in logEntry, buf);

        var formatted = buf.WrittenMemory.ToArray();

        await _stream.WriteAsync(formatted, ct);
        await _stream.FlushAsync(ct);
    }

    protected override async ValueTask OnDisposeAsync()
    {
        await _stream.FlushAsync();
        if (!_leaveOpen)
            await _stream.DisposeAsync();
    }
}
