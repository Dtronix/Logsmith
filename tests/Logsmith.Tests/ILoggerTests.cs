using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class ILoggerTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    private static ILogger CreateLogger(RecordingSink sink)
    {
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });
        return LogManager.GetLogger("Test");
    }

    // ── Terminal method tests ───────────────────────────────────────────

    [Test]
    public void Trace_DispatchesAtTraceLevel()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });
        var logger = LogManager.GetLogger("Test");

        logger.Trace("trace msg");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Trace));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("trace msg"));
    }

    [Test]
    public void Debug_DispatchesAtDebugLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Debug("debug msg");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Debug));
    }

    [Test]
    public void Information_DispatchesAtInfoLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Information("info msg");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Information));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("info msg"));
    }

    [Test]
    public void Warning_DispatchesAtWarningLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Warning("warn msg");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void Error_DispatchesAtErrorLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Error("error msg");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Error));
    }

    [Test]
    public void Critical_DispatchesAtCriticalLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Critical("critical msg");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Critical));
    }

    [Test]
    public void Debug_WithException_IncludesException()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var ex = new InvalidOperationException("test error");

        logger.Debug("debug msg", ex);

        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
    }

    [Test]
    public void Error_WithException_IncludesException()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var ex = new ArgumentException("bad arg");

        logger.Error("error msg", ex);

        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("error msg"));
    }

    [Test]
    public void Terminal_BelowMinimumLevel_DoesNotDispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Warning;
            c.AddSink(sink);
        });
        var logger = LogManager.GetLogger("Test");

        logger.Debug("filtered");

        Assert.That(sink.Entries, Is.Empty);
    }

    // ── IsEnabled tests ─────────────────────────────────────────────────

    [Test]
    public void IsEnabled_DelegatesToContext()
    {
        LogManager.Initialize(c => c.MinimumLevel = LogLevel.Warning);
        var logger = LogManager.GetLogger("Test");

        Assert.That(logger.IsEnabled(LogLevel.Debug), Is.False);
        Assert.That(logger.IsEnabled(LogLevel.Warning), Is.True);
    }

    // ── Chain method tests ──────────────���────────────────────��──────────

    [Test]
    public void When_True_ReturnsSameLogger()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        var result = logger.When(true);

        Assert.That(result, Is.SameAs(logger));
    }

    [Test]
    public void When_False_ReturnsNullLogger()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        var result = logger.When(false);

        Assert.That(result, Is.SameAs(NullLogger.Instance));
    }

    [Test]
    public void When_False_SuppressesDispatch()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.When(false).Information("should not appear");

        Assert.That(sink.Entries, Is.Empty);
    }

    [Test]
    public void Sampled_Default_ReturnsSameLogger()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        Assert.That(logger.Sampled(10), Is.SameAs(logger));
    }

    [Test]
    public void RateLimited_Default_ReturnsSameLogger()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        Assert.That(logger.RateLimited(100), Is.SameAs(logger));
    }

    [Test]
    public void Tagged_Default_ReturnsSameLogger()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        Assert.That(logger.Tagged("SQL"), Is.SameAs(logger));
    }

    // ── Hierarchy tests ─────────────────────────────────────────────────

    [Test]
    public void CreateChild_ReturnsNewLogger()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        var child = logger.CreateChild("child");

        Assert.That(child, Is.Not.SameAs(logger));
    }

    [Test]
    public void CreateChild_ChildDispatchesWithPath()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        var child = logger.CreateChild("Handler");
        child.Information("from child");

        Assert.That(sink.Entries[0].Path, Is.EqualTo("Handler"));
    }

    [Test]
    public void PathSegment_GetSet()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        var child = logger.CreateChild("initial");
        Assert.That(child.PathSegment, Is.EqualTo("initial"));

        child.PathSegment = "changed";
        Assert.That(child.PathSegment, Is.EqualTo("changed"));
    }

    [Test]
    public void PathSegment_MutationReflectsInDispatch()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        var child = logger.CreateChild("before");
        child.PathSegment = "after";
        child.Information("msg");

        Assert.That(sink.Entries[0].Path, Is.EqualTo("after"));
    }

    // ── Category propagation ────────────────────────────────────────────

    [Test]
    public void Logger_DispatchesWithCorrectCategory()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var logger = LogManager.GetLogger("MyService");
        logger.Information("hello");

        Assert.That(sink.Entries[0].Category, Is.EqualTo("MyService"));
    }
}
