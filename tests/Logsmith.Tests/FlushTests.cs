using System.Text;
using Logsmith.Formatting;
using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class FlushTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public async Task FlushAsync_EntriesBeforeFlush_AreWritten()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);

        var info = MakeInfo("before-flush");
        sink.Write(in info);

        await sink.FlushAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("before-flush"));

        await sink.DisposeAsync();
    }

    [Test]
    public async Task FlushAsync_EntriesAfterFlush_AreNotBlocked()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);

        var info1 = MakeInfo("before");
        sink.Write(in info1);
        await sink.FlushAsync();

        var info2 = MakeInfo("after");
        sink.Write(in info2);
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("before"));
        Assert.That(content, Does.Contain("after"));
    }

    [Test]
    public async Task FlushAsync_WithTimeout_CancelsCleanly()
    {
        var sink = new SlowBufferedSink(writeDelay: TimeSpan.FromSeconds(10));

        var info = MakeInfo("slow-entry");
        sink.Write(in info);

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await sink.FlushAsync(new CancellationTokenSource(TimeSpan.FromMilliseconds(100)).Token));

        await sink.DisposeAsync();
    }

    [Test]
    public async Task FlushAsync_EmptyChannel_CompletesImmediately()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);

        // Flush with nothing enqueued — should complete instantly
        var flushTask = sink.FlushAsync().AsTask();
        var completed = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.That(completed, Is.SameAs(flushTask));

        await sink.DisposeAsync();
    }

    [Test]
    public async Task FlushAsync_AfterDispose_CompletesImmediately()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);

        await sink.DisposeAsync();

        // Channel is completed — flush should complete immediately
        var flushTask = sink.FlushAsync().AsTask();
        var completed = await Task.WhenAny(flushTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.That(completed, Is.SameAs(flushTask));
    }

    [Test]
    public async Task LogManager_FlushAsync_SkipsNonFlushableSinks()
    {
        var recording = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(recording));

        DispatchTestMessage(LogLevel.Information, "test");

        // RecordingSink does not implement IFlushableLogSink — should not throw
        Assert.DoesNotThrowAsync(async () => await LogManager.FlushAsync());
    }

    [Test]
    public async Task LogManager_FlushAsync_FlushesBufferedSinks()
    {
        using var ms = new MemoryStream();
        LogManager.Initialize(c =>
            c.AddStreamSink(ms, leaveOpen: true, formatter: NullLogFormatter.Instance));

        DispatchTestMessage(LogLevel.Information, "flush-via-manager");

        await LogManager.FlushAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("flush-via-manager"));
    }

    [Test]
    public async Task LogManager_FlushAsync_WithTimeout_CompletesOrCancels()
    {
        using var ms = new MemoryStream();
        LogManager.Initialize(c =>
            c.AddStreamSink(ms, leaveOpen: true, formatter: NullLogFormatter.Instance));

        DispatchTestMessage(LogLevel.Information, "timed-flush");

        // Short timeout — should still complete since the stream sink is fast
        await LogManager.FlushAsync(TimeSpan.FromSeconds(5));

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("timed-flush"));
    }

    [Test]
    public async Task LogManager_FlushAsync_WhenNotInitialized_IsNoOp()
    {
        // No Initialize called — should not throw
        Assert.DoesNotThrowAsync(async () => await LogManager.FlushAsync());
    }

    [Test]
    public async Task FlushAsync_MultipleFlushes_AllComplete()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);

        var info1 = MakeInfo("msg1");
        sink.Write(in info1);
        await sink.FlushAsync();

        var info2 = MakeInfo("msg2");
        sink.Write(in info2);
        await sink.FlushAsync();

        var info3 = MakeInfo("msg3");
        sink.Write(in info3);
        await sink.FlushAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("msg1"));
        Assert.That(content, Does.Contain("msg2"));
        Assert.That(content, Does.Contain("msg3"));

        await sink.DisposeAsync();
    }

    private static DispatchInfo MakeInfo(string message) => new()
    {
        Level = LogLevel.Information,
        EventId = 1,
        TimestampTicks = DateTime.UtcNow.Ticks,
        Category = "Test",
        Utf8Message = Encoding.UTF8.GetBytes(message),
    };

    private static void DispatchTestMessage(LogLevel level, string message, string category = "Test")
    {
        if (!LogManager.IsEnabled(level, category))
            return;

        var info = new DispatchInfo
        {
            Level = level,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = category,
            Utf8Message = Encoding.UTF8.GetBytes(message),
        };

        LogManager.Dispatch(in info);
    }

    private sealed class SlowBufferedSink : BufferedLogSink
    {
        private readonly TimeSpan _writeDelay;

        public SlowBufferedSink(TimeSpan writeDelay)
            : base(LogLevel.Trace)
        {
            _writeDelay = writeDelay;
        }

        protected override async Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct)
        {
            await Task.Delay(_writeDelay, ct);
        }
    }
}
