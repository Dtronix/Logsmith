using System.Text;
using System.Text.Json;
using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class LogManagerTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void Initialize_WithSink_DispatchesMessages()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        DispatchTestMessage(LogLevel.Information, "hello");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("hello"));
    }

    [Test]
    public void Initialize_MinimumLevel_FiltersBelow()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Warning;
            c.AddSink(sink);
        });

        DispatchTestMessage(LogLevel.Debug, "filtered");
        DispatchTestMessage(LogLevel.Warning, "visible");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("visible"));
    }

    [Test]
    public void Reconfigure_SwapsConfig_NewSinkReceivesMessages()
    {
        var oldSink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(oldSink));

        var newSink = new RecordingSink();
        LogManager.Reconfigure(c => c.AddSink(newSink));

        DispatchTestMessage(LogLevel.Information, "after reconfig");

        Assert.That(newSink.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void Reconfigure_OldSinkStopsReceiving()
    {
        var oldSink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(oldSink));

        DispatchTestMessage(LogLevel.Information, "before");

        var newSink = new RecordingSink();
        LogManager.Reconfigure(c => c.AddSink(newSink));

        DispatchTestMessage(LogLevel.Information, "after");

        Assert.That(oldSink.Entries, Has.Count.EqualTo(1));
        Assert.That(oldSink.Entries[0].Message, Is.EqualTo("before"));
    }

    [Test]
    public void IsEnabled_BelowMinimum_ReturnsFalse()
    {
        LogManager.Initialize(c => c.MinimumLevel = LogLevel.Warning);
        Assert.That(LogManager.IsEnabled(LogLevel.Debug), Is.False);
    }

    [Test]
    public void IsEnabled_AtMinimum_ReturnsTrue()
    {
        LogManager.Initialize(c => c.MinimumLevel = LogLevel.Warning);
        Assert.That(LogManager.IsEnabled(LogLevel.Warning), Is.True);
    }

    [Test]
    public void IsEnabled_AboveMinimum_ReturnsTrue()
    {
        LogManager.Initialize(c => c.MinimumLevel = LogLevel.Warning);
        Assert.That(LogManager.IsEnabled(LogLevel.Error), Is.True);
    }

    [Test]
    public void Dispatch_TextSink_ReceivesUtf8Message()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        DispatchTestMessage(LogLevel.Information, "utf8 test");

        Assert.That(sink.Entries[0].Message, Is.EqualTo("utf8 test"));
    }

    [Test]
    public void Dispatch_MultipleSinks_AllReceiveMessage()
    {
        var sink1 = new RecordingSink();
        var sink2 = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.AddSink(sink1);
            c.AddSink(sink2);
        });

        DispatchTestMessage(LogLevel.Information, "broadcast");

        Assert.That(sink1.Entries, Has.Count.EqualTo(1));
        Assert.That(sink2.Entries, Has.Count.EqualTo(1));
    }

    private static void DispatchTestMessage(LogLevel level, string message)
    {
        if (!LogManager.IsEnabled(level))
            return;

        var entry = new LogEntry(
            level: level,
            eventId: 1,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: "Test");

        var utf8 = Encoding.UTF8.GetBytes(message).AsSpan();

        LogManager.Dispatch(in entry, utf8, 0, static (writer, state) => { });
    }
}
