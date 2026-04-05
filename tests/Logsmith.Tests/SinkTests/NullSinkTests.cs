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
        var info = MakeInfo();
        sink.Write(in info);
        Assert.Pass();
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var sink = new NullSink();
        Assert.DoesNotThrow(() => sink.Dispose());
    }

    private static DispatchInfo MakeInfo() => new()
    {
        Level = LogLevel.Information,
        EventId = 1,
        TimestampTicks = DateTime.UtcNow.Ticks,
        Category = "Test",
    };
}
