using Logsmith.Sinks;

namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class NullSinkTests
{
    [Test]
    public void IsEnabled_AlwaysReturnsFalse()
    {
        using var sink = new NullSink();
        Assert.That(sink.IsEnabled(LogLevel.Trace), Is.False);
        Assert.That(sink.IsEnabled(LogLevel.Critical), Is.False);
    }

    [Test]
    public void Write_DoesNotThrow()
    {
        using var sink = new NullSink();
        var entry = MakeEntry();
        Assert.DoesNotThrow(() => sink.Write(in entry, "test"u8));
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var sink = new NullSink();
        Assert.DoesNotThrow(() => sink.Dispose());
    }

    private static LogEntry MakeEntry() => new(
        LogLevel.Information, 1, DateTime.UtcNow.Ticks, "Test");
}
