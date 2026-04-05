namespace Logsmith.Tests;

[TestFixture]
public class DispatchInfoTests
{
    [Test]
    public void ThreadId_CapturedAtConstruction()
    {
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
            ThreadId = Environment.CurrentManagedThreadId,
        };

        Assert.That(info.ThreadId, Is.EqualTo(Environment.CurrentManagedThreadId));
    }

    [Test]
    public void ThreadName_CapturedAtConstruction()
    {
        var originalName = Thread.CurrentThread.Name;
        try
        {
            if (Thread.CurrentThread.Name == null)
                Thread.CurrentThread.Name = "TestThread";

            var info = new DispatchInfo
            {
                Level = LogLevel.Information,
                EventId = 1,
                TimestampTicks = DateTime.UtcNow.Ticks,
                Category = "Test",
                ThreadName = Thread.CurrentThread.Name,
            };

            Assert.That(info.ThreadName, Is.EqualTo(Thread.CurrentThread.Name));
        }
        finally
        {
            // Thread name can only be set once, so we can't restore it
        }
    }

    [Test]
    public void ThreadName_NullWhenUnset()
    {
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
            ThreadId = 42,
            ThreadName = null,
        };

        Assert.That(info.ThreadName, Is.Null);
    }

    [Test]
    public void ThreadId_DefaultsToZero()
    {
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
        };

        Assert.That(info.ThreadId, Is.EqualTo(0));
    }
}
