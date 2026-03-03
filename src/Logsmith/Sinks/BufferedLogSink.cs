using System.Buffers;
using System.Threading.Channels;

namespace Logsmith.Sinks;

public abstract class BufferedLogSink : ILogSink, IFlushableLogSink, IAsyncDisposable
{
    protected readonly record struct BufferedEntry(
        LogEntry Entry,
        byte[] Utf8MessageBuffer,
        int Utf8MessageLength,
        TaskCompletionSource? FlushCompletion = null)
    {
        internal bool IsFlushSentinel => FlushCompletion is not null;

        internal static BufferedEntry CreateFlushSentinel(TaskCompletionSource tcs)
            => new(default, Array.Empty<byte>(), 0, tcs);
    };

    protected LogLevel MinimumLevel { get; }

    private readonly Channel<BufferedEntry> _channel;
    private readonly Task _drainTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _drainTimeout;

    protected BufferedLogSink(LogLevel minimumLevel = LogLevel.Trace, int capacity = 1024,
                              TimeSpan? drainTimeout = null)
    {
        MinimumLevel = minimumLevel;
        _drainTimeout = drainTimeout ?? TimeSpan.FromSeconds(30);
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

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sentinel = BufferedEntry.CreateFlushSentinel(tcs);

        if (!_channel.Writer.TryWrite(sentinel))
            tcs.SetResult(); // channel completed or full — nothing to flush

        return new ValueTask(tcs.Task.WaitAsync(cancellationToken));
    }

    private async Task DrainAsync()
    {
        try
        {
            await foreach (var entry in _channel.Reader.ReadAllAsync(_cts.Token))
            {
                if (entry.IsFlushSentinel)
                {
                    entry.FlushCompletion!.SetResult();
                    continue;
                }

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

        if (_drainTimeout == Timeout.InfiniteTimeSpan)
        {
            await _drainTask;
        }
        else
        {
            using var timeoutCts = new CancellationTokenSource(_drainTimeout);
            try
            {
                await _drainTask.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Drain timed out — cancel the drain loop so it stops processing
                await _cts.CancelAsync();
            }
        }

        _cts.Dispose();
        await OnDisposeAsync();
    }
}
