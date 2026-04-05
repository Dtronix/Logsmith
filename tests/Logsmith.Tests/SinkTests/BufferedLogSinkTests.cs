using System.Text;
using Logsmith.Formatting;
using Logsmith.Sinks;

namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class BufferedLogSinkTests
{
    [Test]
    public async Task Write_MessageIntegrity_RentedBufferDeliversCorrectBytes()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);
        var message = "The quick brown fox jumps over the lazy dog \U0001F98A";
        var info = MakeInfo(message);

        sink.Write(in info);
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Is.EqualTo(message));
    }

    [Test]
    public async Task Write_LargeMessage_PreservedThroughPool()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);
        var message = new string('A', 4096);
        var info = MakeInfo(message);

        sink.Write(in info);
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Is.EqualTo(message));
    }

    [Test]
    public async Task Write_ManyMessages_AllPreservedCorrectly()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);
        var messages = new List<string>();

        for (int i = 0; i < 100; i++)
        {
            var msg = $"message-{i:D4}";
            messages.Add(msg);
            var info = MakeInfo(msg);
            sink.Write(in info);
        }

        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        foreach (var msg in messages)
            Assert.That(content, Does.Contain(msg));
    }

    [Test]
    public async Task Write_EntryFieldsPreserved_ThroughEmbeddedLogEntry()
    {
        using var ms = new MemoryStream();
        // Use a formatter that includes date so we can verify timestamp roundtrips
        var sink = new StreamSink(ms, formatter: new DefaultLogFormatter(includeDate: true), leaveOpen: true);

        var ticks = new DateTime(2025, 6, 15, 10, 30, 45, DateTimeKind.Utc).Ticks;
        var info = new DispatchInfo
        {
            Level = LogLevel.Warning,
            EventId = 42,
            TimestampTicks = ticks,
            Category = "MyCategory",
            Utf8Message = "hello"u8,
            CallerFile = "test.cs",
            CallerLine = 99,
            CallerMember = "TestMethod",
            ThreadId = 7,
            ThreadName = "worker",
        };

        sink.Write(in info);
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("WRN"));
        Assert.That(content, Does.Contain("MyCategory"));
        Assert.That(content, Does.Contain("2025-06-15"));
        Assert.That(content, Does.Contain("hello"));
    }

    [Test]
    public void Write_BackPressure_DoesNotThrow()
    {
        // Create a sink with capacity=1 and a slow consumer to force back-pressure
        using var ms = new MemoryStream();
        var sink = new SlowStreamSink(ms, capacity: 1);

        // Rapidly write more messages than the channel can hold
        // Some TryWrite calls will fail — should not throw
        for (int i = 0; i < 50; i++)
        {
            var info = MakeInfo("pressure");
            sink.Write(in info);
        }

        Assert.Pass();
        sink.Dispose();
    }

    [Test]
    public void Write_ChannelFull_IncrementsDroppedCount()
    {
        using var ms = new MemoryStream();
        var sink = new SlowStreamSink(ms, capacity: 1);

        // Fill the channel and force drops
        for (int i = 0; i < 50; i++)
        {
            var info = MakeInfo("drop-test");
            sink.Write(in info);
        }

        Assert.That(sink.DroppedCount, Is.GreaterThan(0));
        sink.Dispose();
    }

    [Test]
    public void Write_ChannelFull_ErrorHandlerCalledWithLogDroppedException()
    {
        var exceptions = new List<Exception>();
        using var ms = new MemoryStream();
        var sink = new SlowStreamSink(ms, capacity: 1, errorHandler: ex => exceptions.Add(ex));

        for (int i = 0; i < 50; i++)
        {
            var info = MakeInfo("handler-test");
            sink.Write(in info);
        }

        Assert.That(exceptions, Is.Not.Empty);
        Assert.That(exceptions[0], Is.TypeOf<LogDroppedException>());
        Assert.That(((LogDroppedException)exceptions[0]).TotalDropped, Is.GreaterThanOrEqualTo(1));
        sink.Dispose();
    }

    [Test]
    public void Write_ChannelFull_ErrorHandlerIsThrottled()
    {
        var callCount = 0;
        using var ms = new MemoryStream();
        var sink = new SlowStreamSink(ms, capacity: 1, errorHandler: _ => Interlocked.Increment(ref callCount));

        // Rapid-fire drops all happen within the same tick window
        for (int i = 0; i < 200; i++)
        {
            var info = MakeInfo("throttle");
            sink.Write(in info);
        }

        // Throttle should limit notifications — far fewer callbacks than drops
        Assert.That(callCount, Is.LessThan(sink.DroppedCount));
        sink.Dispose();
    }

    [Test]
    public void Write_ChannelFull_ConcurrentDropCountIsAccurate()
    {
        using var ms = new MemoryStream();
        var sink = new SlowStreamSink(ms, capacity: 1);
        var barrier = new Barrier(4);

        var tasks = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            barrier.SignalAndWait();
            for (int i = 0; i < 100; i++)
            {
                var info = MakeInfo("concurrent");
                sink.Write(in info);
            }
        })).ToArray();

        Task.WaitAll(tasks);

        // DroppedCount should be consistent (>0 since capacity=1 with slow consumer and 4 threads)
        Assert.That(sink.DroppedCount, Is.GreaterThan(0));
        sink.Dispose();
    }

    [Test]
    public async Task Write_ChannelNotFull_NoDropsReported()
    {
        using var ms = new MemoryStream();
        var errorCalled = false;
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true,
            capacity: 1024, errorHandler: _ => errorCalled = true);
        var info = MakeInfo("no-drop");

        sink.Write(in info);
        await sink.DisposeAsync();

        Assert.That(sink.DroppedCount, Is.EqualTo(0));
        Assert.That(errorCalled, Is.False);
    }

    [Test]
    public void LogDroppedException_TotalDropped_ReflectsCount()
    {
        var ex = new LogDroppedException(42);
        Assert.That(ex.TotalDropped, Is.EqualTo(42));
        Assert.That(ex.Message, Does.Contain("42"));
    }

    private static DispatchInfo MakeInfo(string message) => new()
    {
        Level = LogLevel.Information,
        EventId = 1,
        TimestampTicks = DateTime.UtcNow.Ticks,
        Category = "Test",
        Utf8Message = Encoding.UTF8.GetBytes(message),
    };

    /// <summary>
    /// A buffered sink with intentional delay to create back-pressure.
    /// </summary>
    private sealed class SlowStreamSink : BufferedLogSink
    {
        private readonly Stream _stream;

        public SlowStreamSink(Stream stream, int capacity = 1,
                              Action<Exception>? errorHandler = null)
            : base(LogLevel.Trace, capacity, errorHandler: errorHandler)
        {
            _stream = stream;
        }

        protected override async Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct)
        {
            await Task.Delay(50, ct);
            await _stream.WriteAsync(entry.Buffer.AsMemory(0, entry.MessageLength), ct);
        }
    }
}
