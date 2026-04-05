using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class DPanicTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void DPanic_String_DispatchesAtErrorLevel()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        logger.DPanic("something unexpected");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Error));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("something unexpected"));
    }

    [Test]
    public void DPanic_String_NoThrowWhenDisabled()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
            c.ThrowOnDPanic = false;
        });

        var logger = LogManager.GetLogger("Test");
        Assert.DoesNotThrow(() => logger.DPanic("should not throw"));
        Assert.That(sink.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void DPanic_String_ThrowsWhenEnabled()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
            c.ThrowOnDPanic = true;
        });

        var logger = LogManager.GetLogger("Test");
        var ex = Assert.Throws<InvalidOperationException>(() => logger.DPanic("panic message"));
        Assert.That(ex!.Message, Is.EqualTo("panic message"));
        Assert.That(sink.Entries, Has.Count.EqualTo(1), "Should dispatch before throwing");
    }

    [Test]
    public void DPanic_StringWithException_DispatchesExceptionAndThrows()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
            c.ThrowOnDPanic = true;
        });

        var logger = LogManager.GetLogger("Test");
        var inner = new ArgumentException("inner");
        var thrown = Assert.Throws<InvalidOperationException>(() => logger.DPanic("panic", inner));

        Assert.That(thrown!.InnerException, Is.SameAs(inner));
        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(inner));
    }

    [Test]
    public void DPanic_StringWithException_NoThrowWhenDisabled()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
            c.ThrowOnDPanic = false;
        });

        var logger = LogManager.GetLogger("Test");
        var inner = new ArgumentException("inner");
        Assert.DoesNotThrow(() => logger.DPanic("panic", inner));
        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(inner));
    }

    [Test]
    public void DPanic_Handler_DispatchesStructuredData()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var code = 42;
        logger.DPanic($"Unexpected state {code}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Error));
        Assert.That(sink.Entries[0].Message, Does.Contain("42"));
        Assert.That(sink.Entries[0].JsonMessage, Does.Contain("\"code\""));
    }

    [Test]
    public void DPanic_Handler_ThrowsWhenEnabled()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
            c.ThrowOnDPanic = true;
        });

        var logger = LogManager.GetLogger("Test");
        var code = 42;
        var ex = Assert.Throws<InvalidOperationException>(() =>
            logger.DPanic($"Unexpected state {code}"));
        Assert.That(ex!.Message, Does.Contain("42"));
        Assert.That(sink.Entries, Has.Count.EqualTo(1), "Should dispatch before throwing");
    }

    [Test]
    public void DPanic_HandlerWithException_ThrowsWhenEnabled()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
            c.ThrowOnDPanic = true;
        });

        var logger = LogManager.GetLogger("Test");
        var inner = new ArgumentException("cause");
        var thrown = Assert.Throws<InvalidOperationException>(() =>
            logger.DPanic(inner, $"Failure due to {inner.Message}"));

        Assert.That(thrown!.InnerException, Is.SameAs(inner));
        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(inner));
    }

    [Test]
    public void DPanic_RespectedByIsEnabled()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Critical;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        logger.DPanic("should not dispatch");

        Assert.That(sink.Entries, Has.Count.EqualTo(0));
    }

    [Test]
    public void DPanic_ThrowsEvenWhenLevelDisabled()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Critical;
            c.AddSink(sink);
            c.ThrowOnDPanic = true;
        });

        var logger = LogManager.GetLogger("Test");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            logger.DPanic("correctness guard"));

        Assert.That(ex!.Message, Is.EqualTo("correctness guard"));
        Assert.That(sink.Entries, Has.Count.EqualTo(0),
            "Log dispatch is filtered by level, but throw still fires");
    }
}
