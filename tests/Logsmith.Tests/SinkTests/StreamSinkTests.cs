using System.Text;
using Logsmith.Formatting;
using Logsmith.Sinks;

namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class StreamSinkTests
{
    [Test]
    public async Task Write_WritesToUnderlyingStream()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, leaveOpen: true);
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
            Utf8Message = "stream test"u8,
        };

        sink.Write(in info);
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("stream test"));
    }

    [Test]
    public async Task Write_UsesFormatter_ForPrefixSuffix()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: new DefaultLogFormatter(includeDate: true), leaveOpen: true);
        var info = MakeInfo("formatted");

        sink.Write(in info);
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        // DefaultLogFormatter with date produces [yyyy-MM-dd HH:mm:ss.fff INF Test]
        Assert.That(content, Does.Match(@"\[\d{4}-\d{2}-\d{2}"));
    }

    [Test]
    public async Task Write_NullFormatter_WritesRawMessage()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);
        var info = MakeInfo("raw only");

        sink.Write(in info);
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Is.EqualTo("raw only"));
    }

    [Test]
    public async Task LeaveOpen_True_DoesNotDisposeStream()
    {
        var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);
        var info = MakeInfo("test");

        sink.Write(in info);
        await sink.DisposeAsync();

        // Stream should still be writable
        Assert.DoesNotThrow(() => ms.WriteByte(0));
        ms.Dispose();
    }

    [Test]
    public async Task LeaveOpen_False_DisposesStream()
    {
        var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: false);
        var info = MakeInfo("test");

        sink.Write(in info);
        await sink.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => ms.WriteByte(0));
    }

    [Test]
    public void IsEnabled_RespectsMinimumLevel()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, minimumLevel: LogLevel.Warning, leaveOpen: true);

        Assert.That(sink.IsEnabled(LogLevel.Debug), Is.False);
        Assert.That(sink.IsEnabled(LogLevel.Warning), Is.True);
        Assert.That(sink.IsEnabled(LogLevel.Error), Is.True);

        sink.Dispose();
    }

    [Test]
    public async Task MultipleWrites_AllAppearInStream()
    {
        using var ms = new MemoryStream();
        var sink = new StreamSink(ms, formatter: NullLogFormatter.Instance, leaveOpen: true);

        var info1 = MakeInfo("one");
        sink.Write(in info1);
        var info2 = MakeInfo("two");
        sink.Write(in info2);
        var info3 = MakeInfo("three");
        sink.Write(in info3);
        await sink.DisposeAsync();

        var content = Encoding.UTF8.GetString(ms.ToArray());
        Assert.That(content, Does.Contain("one"));
        Assert.That(content, Does.Contain("two"));
        Assert.That(content, Does.Contain("three"));
    }

    private static DispatchInfo MakeInfo(string message) => new()
    {
        Level = LogLevel.Information,
        EventId = 1,
        TimestampTicks = DateTime.UtcNow.Ticks,
        Category = "Test",
        Utf8Message = Encoding.UTF8.GetBytes(message),
    };
}
