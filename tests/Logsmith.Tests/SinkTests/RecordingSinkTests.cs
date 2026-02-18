using Logsmith.Sinks;

namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class RecordingSinkTests
{
    [Test]
    public void Write_CapturesEntry()
    {
        using var sink = new RecordingSink();
        var entry = MakeEntry();
        sink.Write(in entry, "hello"u8);

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void Write_CapturesUtf8MessageAsString()
    {
        using var sink = new RecordingSink();
        var entry = MakeEntry();
        sink.Write(in entry, "hello world"u8);

        Assert.That(sink.Entries[0].Message, Is.EqualTo("hello world"));
    }

    [Test]
    public void Write_MultipleEntries_AllCaptured()
    {
        using var sink = new RecordingSink();
        var entry = MakeEntry();
        sink.Write(in entry, "one"u8);
        sink.Write(in entry, "two"u8);
        sink.Write(in entry, "three"u8);

        Assert.That(sink.Entries, Has.Count.EqualTo(3));
    }

    [Test]
    public void Clear_RemovesAllEntries()
    {
        using var sink = new RecordingSink();
        var entry = MakeEntry();
        sink.Write(in entry, "one"u8);
        sink.Write(in entry, "two"u8);

        sink.Clear();

        Assert.That(sink.Entries, Is.Empty);
    }

    [Test]
    public void IsEnabled_BelowMinimum_ReturnsFalse()
    {
        using var sink = new RecordingSink(LogLevel.Warning);
        Assert.That(sink.IsEnabled(LogLevel.Debug), Is.False);
    }

    [Test]
    public void IsEnabled_AtOrAboveMinimum_ReturnsTrue()
    {
        using var sink = new RecordingSink(LogLevel.Warning);
        Assert.That(sink.IsEnabled(LogLevel.Warning), Is.True);
        Assert.That(sink.IsEnabled(LogLevel.Error), Is.True);
    }

    [Test]
    public void CapturedEntry_ContainsAllLogEntryFields()
    {
        using var sink = new RecordingSink();
        var ex = new InvalidOperationException("test");
        var entry = new LogEntry(
            level: LogLevel.Error,
            eventId: 42,
            timestampTicks: 12345L,
            category: "MyCategory",
            exception: ex,
            callerFile: "File.cs",
            callerLine: 99,
            callerMember: "MyMethod");

        sink.Write(in entry, "msg"u8);

        var captured = sink.Entries[0];
        Assert.That(captured.Level, Is.EqualTo(LogLevel.Error));
        Assert.That(captured.EventId, Is.EqualTo(42));
        Assert.That(captured.TimestampTicks, Is.EqualTo(12345L));
        Assert.That(captured.Category, Is.EqualTo("MyCategory"));
        Assert.That(captured.Exception, Is.SameAs(ex));
        Assert.That(captured.CallerFile, Is.EqualTo("File.cs"));
        Assert.That(captured.CallerLine, Is.EqualTo(99));
        Assert.That(captured.CallerMember, Is.EqualTo("MyMethod"));
        Assert.That(captured.Message, Is.EqualTo("msg"));
    }

    [Test]
    public void CapturedEntry_IncludesThreadInfo()
    {
        using var sink = new RecordingSink();
        var entry = new LogEntry(
            level: LogLevel.Information,
            eventId: 1,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: "Test",
            threadId: 42,
            threadName: "WorkerThread");

        sink.Write(in entry, "msg"u8);

        var captured = sink.Entries[0];
        Assert.That(captured.ThreadId, Is.EqualTo(42));
        Assert.That(captured.ThreadName, Is.EqualTo("WorkerThread"));
    }

    private static LogEntry MakeEntry() => new(
        LogLevel.Information, 1, DateTime.UtcNow.Ticks, "Test");
}
