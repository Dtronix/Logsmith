using System.Threading.Channels;

namespace Logsmith.Sinks;

public abstract class BufferedLogSink : ILogSink, IAsyncDisposable
{
    protected readonly record struct BufferedEntry(
        LogLevel Level,
        int EventId,
        long TimestampTicks,
        string Category,
        Exception? Exception,
        string? CallerFile,
        int CallerLine,
        string? CallerMember,
        byte[] Utf8Message);

    protected LogLevel MinimumLevel { get; }

    private readonly Channel<BufferedEntry> _channel;
    private readonly Task _drainTask;
    private readonly CancellationTokenSource _cts = new();

    protected BufferedLogSink(LogLevel minimumLevel = LogLevel.Trace, int capacity = 1024)
    {
        MinimumLevel = minimumLevel;
        _channel = Channel.CreateBounded<BufferedEntry>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _drainTask = Task.Run(DrainAsync);
    }

    public virtual bool IsEnabled(LogLevel level) => level >= MinimumLevel;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        var messageCopy = utf8Message.ToArray();
        var buffered = new BufferedEntry(
            entry.Level,
            entry.EventId,
            entry.TimestampTicks,
            entry.Category,
            entry.Exception,
            entry.CallerFile,
            entry.CallerLine,
            entry.CallerMember,
            messageCopy);

        _channel.Writer.TryWrite(buffered);
    }

    protected abstract Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct);

    private async Task DrainAsync()
    {
        try
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                await WriteBufferedAsync(entry, _cts.Token);
            }
        }
        catch (OperationCanceledException) { }
    }

    protected virtual ValueTask OnDisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        await _drainTask;
        _cts.Dispose();
        await OnDisposeAsync();
    }
}
