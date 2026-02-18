namespace Logsmith.Tests;

[TestFixture]
public class LogEntryTests
{
    [Test]
    public void ThreadId_CapturedAtConstruction()
    {
        var entry = new LogEntry(
            level: LogLevel.Information,
            eventId: 1,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: "Test",
            threadId: Environment.CurrentManagedThreadId);

        Assert.That(entry.ThreadId, Is.EqualTo(Environment.CurrentManagedThreadId));
    }

    [Test]
    public void ThreadName_CapturedAtConstruction()
    {
        var originalName = Thread.CurrentThread.Name;
        try
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "TestThread";

            var entry = new LogEntry(
                level: LogLevel.Information,
                eventId: 1,
                timestampTicks: DateTime.UtcNow.Ticks,
                category: "Test",
                threadName: Thread.CurrentThread.Name);

            Assert.That(entry.ThreadName, Is.EqualTo(Thread.CurrentThread.Name));
        }
        finally
        {
            // Thread name can only be set once, so we can't restore it
        }
    }

    [Test]
    public void ThreadName_NullWhenUnset()
    {
        var entry = new LogEntry(
            level: LogLevel.Information,
            eventId: 1,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: "Test",
            threadId: 42,
            threadName: null);

        Assert.That(entry.ThreadName, Is.Null);
    }

    [Test]
    public void ThreadId_DefaultsToZero()
    {
        var entry = new LogEntry(
            level: LogLevel.Information,
            eventId: 1,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: "Test");

        Assert.That(entry.ThreadId, Is.EqualTo(0));
    }
}
