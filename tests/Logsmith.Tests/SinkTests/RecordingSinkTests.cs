using Logsmith.Sinks;

namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class RecordingSinkTests
{
    [Test]
    public void Write_CapturesEntry()
    {
        using var sink = new RecordingSink();
        var info = MakeInfo("hello");
        sink.Write(in info);

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void Write_CapturesUtf8MessageAsString()
    {
        using var sink = new RecordingSink();
        var info = MakeInfo("hello world");
        sink.Write(in info);

        Assert.That(sink.Entries[0].Message, Is.EqualTo("hello world"));
    }

    [Test]
    public void Write_MultipleEntries_AllCaptured()
    {
        using var sink = new RecordingSink();
        var info1 = MakeInfo("one");
        sink.Write(in info1);
        var info2 = MakeInfo("two");
        sink.Write(in info2);
        var info3 = MakeInfo("three");
        sink.Write(in info3);

        Assert.That(sink.Entries, Has.Count.EqualTo(3));
    }

    [Test]
    public void Clear_RemovesAllEntries()
    {
        using var sink = new RecordingSink();
        var info1 = MakeInfo("one");
        sink.Write(in info1);
        var info2 = MakeInfo("two");
        sink.Write(in info2);

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
        var info = new DispatchInfo
        {
            Level = LogLevel.Error,
            EventId = 42,
            TimestampTicks = 12345L,
            Category = "MyCategory",
            Utf8Message = "msg"u8,
            Exception = ex,
            CallerFile = "File.cs",
            CallerLine = 99,
            CallerMember = "MyMethod",
        };

        sink.Write(in info);

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
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
            Utf8Message = "msg"u8,
            ThreadId = 42,
            ThreadName = "WorkerThread",
        };

        sink.Write(in info);

        var captured = sink.Entries[0];
        Assert.That(captured.ThreadId, Is.EqualTo(42));
        Assert.That(captured.ThreadName, Is.EqualTo("WorkerThread"));
    }

    private static DispatchInfo MakeInfo(string message) => new()
    {
        Level = LogLevel.Information,
        EventId = 1,
        TimestampTicks = DateTime.UtcNow.Ticks,
        Category = "Test",
        Utf8Message = System.Text.Encoding.UTF8.GetBytes(message),
    };
}
