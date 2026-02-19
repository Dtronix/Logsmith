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
        var entry = MakeEntry();
        var message = "The quick brown fox jumps over the lazy dog 🦊";

        sink.Write(in entry, Encoding.UTF8.GetBytes(message));
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Is.EqualTo(message));
    }

    [Test]
    public async Task Write_LargeMessage_PreservedThroughPool()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);
        var entry = MakeEntry();
        var message = new string('A', 4096);

        sink.Write(in entry, Encoding.UTF8.GetBytes(message));
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Is.EqualTo(message));
    }

    [Test]
    public async Task Write_ManyMessages_AllPreservedCorrectly()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);
        var entry = MakeEntry();
        var messages = new List<string>();

        for (int i = 0; i < 100; i++)
        {
            var msg = $"message-{i:D4}";
            messages.Add(msg);
            sink.Write(in entry, Encoding.UTF8.GetBytes(msg));
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
        var entry = new LogEntry(
            LogLevel.Warning, 42, ticks, "MyCategory",
            callerFile: "test.cs", callerLine: 99, callerMember: "TestMethod",
            threadId: 7, threadName: "worker");

        sink.Write(in entry, "hello"u8);
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
        var entry = MakeEntry();

        // Rapidly write more messages than the channel can hold
        // Some TryWrite calls will fail — should not throw
        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 50; i++)
                sink.Write(in entry, "pressure"u8);
        });

        sink.Dispose();
    }

    private static LogEntry MakeEntry() => new(
        LogLevel.Information, 1, DateTime.UtcNow.Ticks, "Test");

    /// <summary>
    /// A buffered sink with intentional delay to create back-pressure.
    /// </summary>
    private sealed class SlowStreamSink : BufferedLogSink
    {
        private readonly Stream _stream;

        public SlowStreamSink(Stream stream, int capacity = 1)
            : base(LogLevel.Trace, capacity)
        {
            _stream = stream;
        }

        protected override async Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct)
        {
            await Task.Delay(50, ct);
            await _stream.WriteAsync(entry.Utf8MessageBuffer.AsMemory(0, entry.Utf8MessageLength), ct);
        }
    }
}
