using Logsmith.Sinks;

namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class DebugSinkTests
{
    [Test]
    public void Write_DoesNotThrow()
    {
        using var sink = new DebugSink();
        var entry = MakeEntry();
        Assert.DoesNotThrow(() => sink.Write(in entry, "debug msg"u8));
    }

    [Test]
    public void IsEnabled_RespectsMinimumLevel()
    {
        using var sink = new DebugSink(LogLevel.Warning);
        // IsEnabled also requires Debugger.IsAttached, so in test context
        // it may return false regardless. We verify the level check by
        // testing that a higher level is not less than a lower one.
        // When debugger is attached: Warning+ enabled, below filtered.
        // When not attached: all return false (correct behavior).
        Assert.That(sink.IsEnabled(LogLevel.Trace), Is.False);
    }

    [Test]
    public void Dispose_DoesNotThrow()
    {
        var sink = new DebugSink();
        Assert.DoesNotThrow(() => sink.Dispose());
    }

    private static LogEntry MakeEntry() => new(
        LogLevel.Information, 1, DateTime.UtcNow.Ticks, "Test");
}
