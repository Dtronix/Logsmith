using Logsmith.Sinks;

namespace Logsmith.Tests;

/// <summary>
/// End-to-end integration tests verifying ILogger API and [LogMessage] coexistence,
/// chain calls with interceptors, and full dispatch pipeline.
/// </summary>
[TestFixture]
public class IntegrationTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void ILogger_InterpolatedString_DispatchesToSink()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.Debug($"Hello {42} world");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("Hello 42 world"));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Debug));
        Assert.That(sink.Entries[0].Category, Is.EqualTo("Test"));
    }

    [Test]
    public void ILogger_InterpolatedString_HasStructuredJson()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        int count = 42;
        logger.Debug($"Processed {count} items");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        // The interceptor passes handler output which includes JSON
        Assert.That(sink.Entries[0].JsonMessage, Is.Not.Null.Or.Empty);
    }

    [Test]
    public void ILogger_String_DispatchesToSink()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.Information("plain message");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("plain message"));
    }

    [Test]
    public void ILogger_WithException_PassesException()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        var ex = new InvalidOperationException("test error");
        logger.Error(ex, $"Failed operation {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
    }

    [Test]
    public void ILogger_When_False_DoesNotDispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.When(false).Debug($"Should not appear {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(0));
    }

    [Test]
    public void ILogger_When_True_Dispatches()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.When(true).Debug($"Conditional message {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("Conditional message"));
    }

    [Test]
    public void ILogger_Tagged_SetsTagOnDispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.Tagged("SQL").Debug($"Query executed {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Tag, Is.EqualTo("SQL"));
    }

    [Test]
    public void ILogger_WhenTagged_Chain_WorksTogether()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.When(true).Tagged("HTTP").Information($"Request {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Tag, Is.EqualTo("HTTP"));
        Assert.That(sink.Entries[0].Message, Does.Contain("Request"));
    }

    [Test]
    public void ILogger_WhenFalse_Tagged_DoesNotDispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.When(false).Tagged("HTTP").Information($"Filtered {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(0));
    }

    [Test]
    public void ILogger_DisabledLevel_DoesNotDispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Warning;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.Debug($"Filtered {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(0));
    }

    [Test]
    public void ILogger_CreateChild_InheritsCategory()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger parent = LogManager.GetLogger("Parent");
        ILogger child = parent.CreateChild("Child");
        child.Debug($"From child {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Category, Is.EqualTo("Parent"));
        Assert.That(sink.Entries[0].Path, Does.Contain("Child"));
    }

    [Test]
    public void ILogger_InterceptorAddsCallerInfo()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.Debug($"Caller test {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        // Interceptor bakes in caller info at compile time
        Assert.That(sink.Entries[0].CallerFile, Is.Not.Null.And.Not.Empty);
        Assert.That(sink.Entries[0].CallerMember, Is.Not.Null.And.Not.Empty);
        Assert.That(sink.Entries[0].CallerLine, Is.GreaterThan(0));
    }

    [Test]
    public void LogMessage_And_ILogger_CoexistOnSameSink()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        // ILogger API
        ILogger logger = LogManager.GetLogger("ILoggerApi");
        logger.Information($"From ILogger {1}");

        // [LogMessage] API
        CoexistenceLog.Greet("World");

        Assert.That(sink.Entries, Has.Count.EqualTo(2));
        Assert.That(sink.Entries[0].Category, Is.EqualTo("ILoggerApi"));
        Assert.That(sink.Entries[1].Category, Is.EqualTo("CoexistenceLog"));
    }

    [Test]
    public void ILogger_AllLevels_Dispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.Trace($"trace {1}");
        logger.Debug($"debug {2}");
        logger.Information($"info {3}");
        logger.Warning($"warn {4}");
        logger.Error($"error {5}");
        logger.Critical($"critical {6}");

        Assert.That(sink.Entries, Has.Count.EqualTo(6));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Trace));
        Assert.That(sink.Entries[1].Level, Is.EqualTo(LogLevel.Debug));
        Assert.That(sink.Entries[2].Level, Is.EqualTo(LogLevel.Information));
        Assert.That(sink.Entries[3].Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(sink.Entries[4].Level, Is.EqualTo(LogLevel.Error));
        Assert.That(sink.Entries[5].Level, Is.EqualTo(LogLevel.Critical));
    }

    [Test]
    public void Scoped_Logger_AppendsPathSegment()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        using var scope = logger.Scoped("Operation");
        ((ILogger)scope).Debug($"In scope {1}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Path, Does.Contain("Operation"));
    }

    [Test]
    public void ILogger_Sampled_DropsMessages()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        // Log 100 messages with Sampled(10) — expect ~10 to get through
        for (int i = 0; i < 100; i++)
            logger.Sampled(10).Debug($"Sampled {i}");

        // With Sampled(10), every 10th message passes (counter % rate == 0)
        Assert.That(sink.Entries.Count, Is.InRange(9, 11));
    }

    [Test]
    public void ILogger_RateLimited_CapsPerSecond()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        ILogger logger = LogManager.GetLogger("Test");
        // Log 50 messages with RateLimited(5) — expect at most ~5 per second
        for (int i = 0; i < 50; i++)
            logger.RateLimited(5).Debug($"Limited {i}");

        Assert.That(sink.Entries.Count, Is.LessThanOrEqualTo(6));
    }

    [Test]
    public void Dispatch_ContinuesAfterSinkThrows()
    {
        var goodSink = new RecordingSink();
        var throwingSink = new ThrowingSink();

        Exception? capturedError = null;
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(throwingSink);
            c.AddSink(goodSink);
            c.InternalErrorHandler = ex => capturedError = ex;
        });

        ILogger logger = LogManager.GetLogger("Test");
        logger.Debug($"Should reach second sink {1}");

        // First sink threw, but second sink should still receive the message
        Assert.That(goodSink.Entries, Has.Count.EqualTo(1));
        Assert.That(goodSink.Entries[0].Message, Does.Contain("Should reach second sink"));
        // Error handler should have been called
        Assert.That(capturedError, Is.Not.Null);
        Assert.That(capturedError, Is.TypeOf<InvalidOperationException>());
    }
}

/// <summary>
/// Sink that always throws, used to test error handling in dispatch.
/// </summary>
internal class ThrowingSink : ILogSink
{
    public bool IsEnabled(LogLevel level) => true;
    public void Write(in DispatchInfo info) => throw new InvalidOperationException("Sink failure");
    public void Dispose() { }
}

/// <summary>
/// Test class for [LogMessage] API coexistence with ILogger.
/// </summary>
public static partial class CoexistenceLog
{
    [LogMessage(LogLevel.Information, "Hello {name}")]
    public static partial void Greet(string name);
}
