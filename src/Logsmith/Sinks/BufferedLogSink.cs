using System.Buffers;
using System.Threading.Channels;

namespace Logsmith.Sinks;

public abstract class BufferedLogSink : ILogSink, IAsyncDisposable
{
    protected readonly record struct BufferedEntry(
        LogEntry Entry,
        byte[] Utf8MessageBuffer,
        int Utf8MessageLength);

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
        var rented = ArrayPool<byte>.Shared.Rent(utf8Message.Length);
        utf8Message.CopyTo(rented);
        var buffered = new BufferedEntry(entry, rented, utf8Message.Length);

        if (!_channel.Writer.TryWrite(buffered))
            ArrayPool<byte>.Shared.Return(rented);
    }

    protected abstract Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct);

    private async Task DrainAsync()
    {
        try
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    await WriteBufferedAsync(entry, _cts.Token);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(entry.Utf8MessageBuffer);
                }
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
