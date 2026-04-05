using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class TimingOperationTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void Complete_LogsCompletion()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("LoadData");
        op.Complete();

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Information));
        Assert.That(sink.Entries[0].Message, Does.Contain("LoadData"));
        Assert.That(sink.Entries[0].Message, Does.Contain("completed"));
    }

    [Test]
    public void Fail_LogsFailure()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("LoadData");
        var ex = new InvalidOperationException("boom");
        op.Fail(ex);

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Error));
        Assert.That(sink.Entries[0].Message, Does.Contain("failed"));
        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
    }

    [Test]
    public void Dispose_WithoutComplete_LogsAbandoned()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("LoadData");
        op.Dispose();

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
        Assert.That(sink.Entries[0].Message, Does.Contain("abandoned"));
    }

    [Test]
    public void Complete_ThenDispose_OnlyLogsOnce()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("LoadData");
        op.Complete();
        op.Dispose();

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Information));
    }

    [Test]
    public void Fail_ThenDispose_OnlyLogsOnce()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("LoadData");
        op.Fail(new Exception("err"));
        op.Dispose();

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void TimeStep_LogsIntermediateStep()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("Pipeline");
        op.TimeStep("Validate");
        op.Complete();

        Assert.That(sink.Entries, Has.Count.EqualTo(2));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Debug));
        Assert.That(sink.Entries[0].Message, Does.Contain("Validate"));
    }

    [Test]
    public void Operation_HasPathSegment()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("LoadData");
        op.Complete();

        Assert.That(sink.Entries[0].Path, Is.EqualTo("LoadData"));
    }

    [Test]
    public void Complete_IncludesElapsedTime()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("Work");
        Thread.Sleep(10); // ensure some elapsed time
        op.Complete();

        Assert.That(sink.Entries[0].Message, Does.Contain("ms"));
    }

    [Test]
    public void UsingBlock_AutoDisposes()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        using (var op = logger.TimeOperation("Work"))
        {
            // don't call Complete
        }

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("abandoned"));
    }
}
