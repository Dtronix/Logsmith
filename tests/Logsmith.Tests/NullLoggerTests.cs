using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class NullLoggerTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void Instance_IsSingleton()
    {
        Assert.That(NullLogger.Instance, Is.SameAs(NullLogger.Instance));
    }

    [Test]
    public void IsEnabled_AlwaysFalse()
    {
        Assert.That(NullLogger.Instance.IsEnabled(LogLevel.Trace), Is.False);
        Assert.That(NullLogger.Instance.IsEnabled(LogLevel.Debug), Is.False);
        Assert.That(NullLogger.Instance.IsEnabled(LogLevel.Information), Is.False);
        Assert.That(NullLogger.Instance.IsEnabled(LogLevel.Warning), Is.False);
        Assert.That(NullLogger.Instance.IsEnabled(LogLevel.Error), Is.False);
        Assert.That(NullLogger.Instance.IsEnabled(LogLevel.Critical), Is.False);
    }

    [Test]
    public void Terminal_DoesNotDispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        ILogger logger = NullLogger.Instance;
        logger.Debug("should not appear");
        logger.Information("should not appear");
        logger.Error("should not appear");

        Assert.That(sink.Entries, Is.Empty);
    }

    [Test]
    public void CreateChild_ReturnsSelf()
    {
        var child = NullLogger.Instance.CreateChild("something");
        Assert.That(child, Is.SameAs(NullLogger.Instance));
    }

    [Test]
    public void PathSegment_GetReturnsNull()
    {
        Assert.That(NullLogger.Instance.PathSegment, Is.Null);
    }

    [Test]
    public void PathSegment_SetIsNoOp()
    {
        NullLogger.Instance.PathSegment = "test";
        Assert.That(NullLogger.Instance.PathSegment, Is.Null);
    }

    [Test]
    public void When_PropagatesNullLogger()
    {
        ILogger logger = NullLogger.Instance;
        var result = logger.When(true);
        // When(true) returns this — which is NullLogger
        Assert.That(result, Is.SameAs(NullLogger.Instance));
    }

    [Test]
    public void Chain_AllReturnSameInstance()
    {
        ILogger logger = NullLogger.Instance;
        Assert.That(logger.Sampled(10), Is.SameAs(NullLogger.Instance));
        Assert.That(logger.RateLimited(100), Is.SameAs(NullLogger.Instance));
        Assert.That(logger.Tagged("SQL"), Is.SameAs(NullLogger.Instance));
    }

    [Test]
    public void Context_IsNotNull()
    {
        Assert.That(NullLogger.Instance.Context, Is.Not.Null);
    }
}
