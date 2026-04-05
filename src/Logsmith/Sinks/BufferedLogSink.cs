using System.Buffers;
using System.Threading.Channels;

namespace Logsmith.Sinks;

public abstract class BufferedLogSink : ILogSink, IFlushableLogSink, IAsyncDisposable
{
    protected readonly record struct BufferedEntry(
        LogLevel Level,
        int EventId,
        long TimestampTicks,
        string Category,
        Exception? Exception,
        string? Tag,
        string? CallerFile,
        int CallerLine,
        string? CallerMember,
        int ThreadId,
        string? ThreadName,
        byte[] Buffer,
        int MessageLength,
        int JsonLength,
        int PathLength,
        TaskCompletionSource? FlushCompletion = null)
    {
        internal bool IsFlushSentinel => FlushCompletion is not null;

        internal static BufferedEntry CreateFlushSentinel(TaskCompletionSource tcs)
            => new(default, default, default, "", null, null, null, default, null, default, null,
                   Array.Empty<byte>(), 0, 0, 0, tcs);

        internal DispatchInfo ToDispatchInfo()
        {
            return new DispatchInfo
            {
                Level = Level,
                EventId = EventId,
                TimestampTicks = TimestampTicks,
                Category = Category,
                Utf8Message = Buffer.AsSpan(0, MessageLength),
                Utf8Json = Buffer.AsSpan(MessageLength, JsonLength),
                Utf8Path = Buffer.AsSpan(MessageLength + JsonLength, PathLength),
                Exception = Exception,
                Tag = Tag,
                CallerFile = CallerFile,
                CallerLine = CallerLine,
                CallerMember = CallerMember,
                ThreadId = ThreadId,
                ThreadName = ThreadName,
            };
        }
    };

    protected LogLevel MinimumLevel { get; }

    private readonly Channel<BufferedEntry> _channel;
    private readonly Task _drainTask;
    private readonly CancellationTokenSource _cts = new();
    private readonly TimeSpan _drainTimeout;
    private readonly Action<Exception>? _errorHandler;

    private long _droppedCount;
    private long _lastDropNotifyTicks;

    /// <summary>
    /// Total number of log messages dropped because the bounded channel was full.
    /// </summary>
    public long DroppedCount => Volatile.Read(ref _droppedCount);

    protected BufferedLogSink(LogLevel minimumLevel = LogLevel.Trace, int capacity = 1024,
                              TimeSpan? drainTimeout = null, Action<Exception>? errorHandler = null)
    {
        MinimumLevel = minimumLevel;
        _drainTimeout = drainTimeout ?? TimeSpan.FromSeconds(30);
        _errorHandler = errorHandler;
        _channel = Channel.CreateBounded<BufferedEntry>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _drainTask = Task.Run(DrainAsync);
    }

    public virtual bool IsEnabled(LogLevel level) => level >= MinimumLevel;

    public void Write(in DispatchInfo info)
    {
        var totalLength = info.Utf8Message.Length + info.Utf8Json.Length + info.Utf8Path.Length;
        var rented = ArrayPool<byte>.Shared.Rent(totalLength);

        info.Utf8Message.CopyTo(rented);
        info.Utf8Json.CopyTo(rented.AsSpan(info.Utf8Message.Length));
        info.Utf8Path.CopyTo(rented.AsSpan(info.Utf8Message.Length + info.Utf8Json.Length));

        var buffered = new BufferedEntry(
            info.Level, info.EventId, info.TimestampTicks, info.Category,
            info.Exception, info.Tag, info.CallerFile, info.CallerLine,
            info.CallerMember, info.ThreadId, info.ThreadName,
            rented, info.Utf8Message.Length, info.Utf8Json.Length, info.Utf8Path.Length);

        if (!_channel.Writer.TryWrite(buffered))
        {
            ArrayPool<byte>.Shared.Return(rented);
            var count = Interlocked.Increment(ref _droppedCount);

            // Notify on first drop, then at most once per second
            var now = Environment.TickCount64;
            var lastNotify = Volatile.Read(ref _lastDropNotifyTicks);
            if (count == 1 || now - lastNotify >= 1000)
            {
                if (Interlocked.CompareExchange(ref _lastDropNotifyTicks, now, lastNotify) == lastNotify)
                {
                    _errorHandler?.Invoke(new LogDroppedException(count));
                }
            }
        }
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
            await foreach (var entry in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
            {
                if (entry.IsFlushSentinel)
                {
                    entry.FlushCompletion!.SetResult();
                    continue;
                }

                try
                {
                    await WriteBufferedAsync(entry, _cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(entry.Buffer);
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
            await _drainTask.ConfigureAwait(false);
        }
        else
        {
            using var timeoutCts = new CancellationTokenSource(_drainTimeout);
            try
            {
                await _drainTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Drain timed out — cancel the drain loop so it stops processing
                await _cts.CancelAsync().ConfigureAwait(false);
            }
        }

        _cts.Dispose();
        await OnDisposeAsync().ConfigureAwait(false);
    }
}
