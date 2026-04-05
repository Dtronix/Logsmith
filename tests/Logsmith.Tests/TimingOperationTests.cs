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

    // ── Structured JSON output tests ───────────────────────────────────

    [Test]
    public void Complete_EmitsStructuredJson()
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

        var json = sink.Entries[0].JsonMessage;
        Assert.That(json, Is.Not.Null);
        Assert.That(json, Does.Contain("\"operation\":\"LoadData\""));
        Assert.That(json, Does.Contain("\"outcome\":\"completed\""));
        Assert.That(json, Does.Contain("\"elapsed_ms\":"));
    }

    [Test]
    public void Fail_EmitsStructuredJson()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("Save");
        op.Fail(new Exception("boom"));

        var json = sink.Entries[0].JsonMessage;
        Assert.That(json, Is.Not.Null);
        Assert.That(json, Does.Contain("\"operation\":\"Save\""));
        Assert.That(json, Does.Contain("\"outcome\":\"failed\""));
        Assert.That(json, Does.Contain("\"elapsed_ms\":"));
    }

    [Test]
    public void TimeStep_EmitsStructuredJsonWithStep()
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

        var stepJson = sink.Entries[0].JsonMessage;
        Assert.That(stepJson, Is.Not.Null);
        Assert.That(stepJson, Does.Contain("\"operation\":\"Pipeline\""));
        Assert.That(stepJson, Does.Contain("\"outcome\":\"step\""));
        Assert.That(stepJson, Does.Contain("\"step\":\"Validate\""));
        Assert.That(stepJson, Does.Contain("\"elapsed_ms\":"));
    }

    [Test]
    public void Abandon_EmitsStructuredJson()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("Work");
        op.Dispose();

        var json = sink.Entries[0].JsonMessage;
        Assert.That(json, Is.Not.Null);
        Assert.That(json, Does.Contain("\"operation\":\"Work\""));
        Assert.That(json, Does.Contain("\"outcome\":\"abandoned\""));
        Assert.That(json, Does.Contain("\"elapsed_ms\":"));
    }

    [Test]
    public void Complete_ElapsedMsIsNumeric()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Test");
        var op = logger.TimeOperation("Work");
        Thread.Sleep(10);
        op.Complete();

        var json = sink.Entries[0].JsonMessage;
        Assert.That(json, Is.Not.Null);
        // Verify elapsed_ms is a number (not a string) — no quotes around the value
        Assert.That(json, Does.Match(@"""elapsed_ms"":\d"));
    }
}
