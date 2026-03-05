using System.Collections.Concurrent;
using System.Text;
using Logsmith.Formatting;
using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class LogManagerShutdownTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public async Task ShutdownAsync_DrainsBufferedEntries()
    {
        using var ms = new MemoryStream();
        LogManager.Initialize(c =>
            c.AddStreamSink(ms, leaveOpen: true, formatter: NullLogFormatter.Instance));

        DispatchTestMessage(LogLevel.Information, "drain-me");

        await LogManager.ShutdownAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("drain-me"));
    }

    [Test]
    public async Task ShutdownAsync_PostShutdownDispatch_IsNoOp()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        await LogManager.ShutdownAsync();

        DispatchTestMessage(LogLevel.Information, "after-shutdown");

        Assert.That(sink.Entries, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task ShutdownAsync_DoubleShutdown_IsIdempotent()
    {
        LogManager.Initialize(c => c.AddSink(new RecordingSink()));

        await LogManager.ShutdownAsync();

        Assert.DoesNotThrowAsync(async () => await LogManager.ShutdownAsync());
    }

    [Test]
    public async Task ShutdownAsync_AllowsReinitialize()
    {
        var sink1 = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink1));
        await LogManager.ShutdownAsync();

        var sink2 = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink2));

        DispatchTestMessage(LogLevel.Information, "after-reinit");

        Assert.That(sink2.Entries, Has.Count.EqualTo(1));
        Assert.That(sink2.Entries[0].Message, Is.EqualTo("after-reinit"));
    }

    [Test]
    public void Shutdown_Sync_DrainsBufferedEntries()
    {
        using var ms = new MemoryStream();
        LogManager.Initialize(c =>
            c.AddStreamSink(ms, leaveOpen: true, formatter: NullLogFormatter.Instance));

        DispatchTestMessage(LogLevel.Information, "sync-drain");

        LogManager.Shutdown();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("sync-drain"));
    }

    [Test]
    public async Task ReconfigureAsync_DisposesOldSinks()
    {
        var disposableSink = new TrackingDisposableSink();
        LogManager.Initialize(c => c.AddSink(disposableSink));

        var newSink = new RecordingSink();
        await LogManager.ReconfigureAsync(c => c.AddSink(newSink));

        Assert.That(disposableSink.Disposed, Is.True);
    }

    [Test]
    public async Task ReconfigureAsync_OldBufferedSink_IsDrained()
    {
        using var ms = new MemoryStream();
        LogManager.Initialize(c =>
            c.AddStreamSink(ms, leaveOpen: true, formatter: NullLogFormatter.Instance));

        DispatchTestMessage(LogLevel.Information, "before-reconfig");

        await LogManager.ReconfigureAsync(c => c.AddSink(new RecordingSink()));

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("before-reconfig"));
    }

    [Test]
    public async Task DrainTimeout_CancelsStuckDrain()
    {
        var sink = new HangingSink(drainTimeout: TimeSpan.FromMilliseconds(200));
        LogManager.Initialize(c => c.AddSink(sink));

        DispatchTestMessage(LogLevel.Information, "will-hang");

        // ShutdownAsync should complete within a reasonable time despite the hanging sink
        var shutdownTask = LogManager.ShutdownAsync().AsTask();
        var completed = await Task.WhenAny(shutdownTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.That(completed, Is.SameAs(shutdownTask), "Shutdown should not hang forever");
    }

    [Test]
    public async Task ShutdownAsync_WithTimeout_CompletesEvenIfSinksAreSlow()
    {
        var sink = new HangingSink(drainTimeout: TimeSpan.FromSeconds(30));
        LogManager.Initialize(c => c.AddSink(sink));

        DispatchTestMessage(LogLevel.Information, "slow-write");

        // Shutdown with a short timeout
        var shutdownTask = LogManager.ShutdownAsync(TimeSpan.FromMilliseconds(200)).AsTask();
        var completed = await Task.WhenAny(shutdownTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.That(completed, Is.SameAs(shutdownTask), "Shutdown with timeout should not hang");
    }

    [Test]
    public async Task ShutdownAsync_DisposesNonBufferedSinks()
    {
        var disposableSink = new TrackingDisposableSink();
        LogManager.Initialize(c => c.AddSink(disposableSink));

        await LogManager.ShutdownAsync();

        Assert.That(disposableSink.Disposed, Is.True);
    }

    private static void DispatchTestMessage(LogLevel level, string message, string category = "Test")
    {
        if (!LogManager.IsEnabled(level, category))
            return;

        var entry = new LogEntry(
            level: level,
            eventId: 1,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: category);

        var utf8 = Encoding.UTF8.GetBytes(message).AsSpan();

        LogManager.Dispatch(in entry, utf8, 0, static (writer, state) => { });
    }

    private sealed class TrackingDisposableSink : ILogSink
    {
        public bool Disposed { get; private set; }

        public bool IsEnabled(LogLevel level) => true;

        public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message) { }

        public void Dispose() => Disposed = true;
    }

    [Test]
    public void Shutdown_Sync_OnSingleThreadedSyncContext_WithBufferedSink_Completes()
    {
        // This test reproduces the STA deadlock: a single-threaded SynchronizationContext
        // (like Avalonia/WPF/WinForms) will deadlock on Shutdown() if the async chain
        // captures and posts back to that context.
        //
        // If this test times out, the deadlock hypothesis is confirmed and
        // ConfigureAwait(false) is needed throughout the async dispose chain.

        using var ms = new MemoryStream();
        LogManager.Initialize(c =>
            c.AddStreamSink(ms, leaveOpen: true, formatter: NullLogFormatter.Instance));

        DispatchTestMessage(LogLevel.Information, "sta-test");

        Exception? caught = null;
        var completed = false;

        var thread = new Thread(() =>
        {
            var ctx = new SingleThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctx);

            ctx.Post(_ =>
            {
                try
                {
                    LogManager.Shutdown();
                    completed = true;
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
                finally
                {
                    ctx.Complete();
                }
            }, null);

            ctx.RunOnCurrentThread();
        });

        thread.IsBackground = true;
        thread.Start();

        var finished = thread.Join(TimeSpan.FromSeconds(10));

        Assert.That(finished, Is.True, "Shutdown() deadlocked on single-threaded SynchronizationContext");
        Assert.That(caught, Is.Null, "Shutdown() threw an unexpected exception");
        Assert.That(completed, Is.True);
    }

    private sealed class HangingSink : BufferedLogSink
    {
        public HangingSink(TimeSpan drainTimeout)
            : base(LogLevel.Trace, capacity: 1024, drainTimeout: drainTimeout) { }

        protected override async Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct)
        {
            // Simulate a hung write that only responds to cancellation
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        }
    }

    /// <summary>
    /// Minimal single-threaded SynchronizationContext that mimics STA dispatchers
    /// (Avalonia, WPF, WinForms). Posts are queued and pumped on the owning thread.
    /// </summary>
    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            _queue.Add((d, state));
        }

        public override void Send(SendOrPostCallback d, object? state)
        {
            // For this test, Send just posts and blocks — matches STA behavior
            var tcs = new TaskCompletionSource();
            _queue.Add((s =>
            {
                try { d(s); tcs.SetResult(); }
                catch (Exception ex) { tcs.SetException(ex); }
            }, state));
            tcs.Task.GetAwaiter().GetResult();
        }

        public void RunOnCurrentThread()
        {
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
                callback(state);
        }

        public void Complete() => _queue.CompleteAdding();
    }
}
