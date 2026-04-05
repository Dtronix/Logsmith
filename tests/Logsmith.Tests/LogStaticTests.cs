using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class LogStaticTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void Trace_ExceptionOverload_CapturesException()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("trace-err");
        Log.Trace(logger, ex, $"Trace with {ex.Message}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Trace));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
        Assert.That(sink.Entries[0].Message, Does.Contain("trace-err"));
    }

    [Test]
    public void Debug_ExceptionOverload_CapturesException()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("debug-err");
        Log.Debug(logger, ex, $"Debug with {ex.Message}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Debug));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
        Assert.That(sink.Entries[0].Message, Does.Contain("debug-err"));
    }

    [Test]
    public void Information_ExceptionOverload_CapturesException()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("info-err");
        Log.Information(logger, ex, $"Info with {ex.Message}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Information));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
        Assert.That(sink.Entries[0].Message, Does.Contain("info-err"));
    }

    [Test]
    public void Warning_ExceptionOverload_CapturesException()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("warn-err");
        Log.Warning(logger, ex, $"Warn with {ex.Message}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
        Assert.That(sink.Entries[0].Message, Does.Contain("warn-err"));
    }

    [Test]
    public void Trace_ExceptionOverload_IncludesStructuredJson()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("err");
        var code = 42;
        Log.Trace(logger, ex, $"Failed with {code}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].JsonMessage, Does.Contain("\"code\""));
        Assert.That(sink.Entries[0].JsonMessage, Does.Contain("42"));
    }

    [Test]
    public void Debug_ExceptionOverload_IncludesStructuredJson()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("err");
        var code = 99;
        Log.Debug(logger, ex, $"Failed with {code}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].JsonMessage, Does.Contain("\"code\""));
        Assert.That(sink.Entries[0].JsonMessage, Does.Contain("99"));
    }

    [Test]
    public void Information_ExceptionOverload_DisabledLevel_NoDispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Warning;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("err");
        Log.Information(logger, ex, $"Should not appear");

        Assert.That(sink.Entries, Has.Count.EqualTo(0));
    }

    [Test]
    public void Warning_ExceptionOverload_IncludesStructuredJson()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("err");
        var retries = 3;
        Log.Warning(logger, ex, $"Retry exhausted after {retries} attempts");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].JsonMessage, Does.Contain("\"retries\""));
        Assert.That(sink.Entries[0].JsonMessage, Does.Contain("3"));
    }
}
