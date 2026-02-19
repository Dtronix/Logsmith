using System.Text;
using Logsmith.Extensions.Logging;
using Logsmith.Sinks;
using Microsoft.Extensions.Logging;

namespace Logsmith.Extensions.Logging.Tests;

[TestFixture]
public class LogsmithLoggerTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void Log_DispatchesToLogsmithSink()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var provider = new LogsmithLoggerProvider();
        var logger = provider.CreateLogger("TestCategory");

        logger.LogInformation("Hello from MEL");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("Hello from MEL"));
        Assert.That(sink.Entries[0].Category, Is.EqualTo("TestCategory"));
    }

    [Test]
    public void Log_RespectsMinimumLevel()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Warning;
            c.AddSink(sink);
        });

        var provider = new LogsmithLoggerProvider();
        var logger = provider.CreateLogger("Test");

        logger.LogDebug("filtered");
        logger.LogWarning("visible");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("visible"));
    }

    [Test]
    public void IsEnabled_MapsLevelsCorrectly()
    {
        LogManager.Initialize(c => c.MinimumLevel = Logsmith.LogLevel.Warning);

        var provider = new LogsmithLoggerProvider();
        var logger = provider.CreateLogger("Test");

        Assert.That(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Debug), Is.False);
        Assert.That(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning), Is.True);
        Assert.That(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Error), Is.True);
    }

    [Test]
    public void BeginScope_PushesLogScope()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var provider = new LogsmithLoggerProvider();
        var logger = provider.CreateLogger("Test");

        using (logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = "abc" }))
        {
            logger.LogInformation("scoped message");
        }

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("[RequestId=abc]"));
    }

    [Test]
    public void BeginScope_StringState_PushesAsScopeProperty()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var provider = new LogsmithLoggerProvider();
        var logger = provider.CreateLogger("Test");

        using (logger.BeginScope("myScope"))
        {
            logger.LogInformation("msg");
        }

        Assert.That(sink.Entries[0].Message, Does.Contain("[Scope=myScope]"));
    }

    [Test]
    public void Log_WithException_PassesToLogEntry()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.AddSink(sink);
        });

        var provider = new LogsmithLoggerProvider();
        var logger = provider.CreateLogger("Test");

        var ex = new InvalidOperationException("test error");
        logger.LogError(ex, "Something failed");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
    }

    [Test]
    public void CreateLogger_CachesSameCategory()
    {
        var provider = new LogsmithLoggerProvider();
        var logger1 = provider.CreateLogger("Same");
        var logger2 = provider.CreateLogger("Same");

        Assert.That(logger1, Is.SameAs(logger2));
    }

    [Test]
    public void Log_RespectsCategoryOverride()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = Logsmith.LogLevel.Debug;
            c.SetMinimumLevel("Noisy", Logsmith.LogLevel.Error);
            c.AddSink(sink);
        });

        var provider = new LogsmithLoggerProvider();
        var noisyLogger = provider.CreateLogger("Noisy");

        noisyLogger.LogWarning("filtered");
        noisyLogger.LogError("visible");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("visible"));
    }
}
