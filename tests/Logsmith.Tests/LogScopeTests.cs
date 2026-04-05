using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class LogScopeTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void Scoped_AddsPathSegment()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var logger = LogManager.GetLogger("Test");
        using var scope = logger.Scoped("Handler");
        ((ILogger)scope).Information("in scope");

        Assert.That(sink.Entries[0].Path, Is.EqualTo("Handler"));
    }

    [Test]
    public void Scoped_DisposesClearsSegment()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var logger = LogManager.GetLogger("Test");
        var scope = logger.Scoped("Temp");
        ((ILogger)scope).Information("before dispose");
        scope.Dispose();
        ((ILogger)scope).Information("after dispose");

        Assert.That(sink.Entries[0].Path, Is.EqualTo("Temp"));
        // After dispose, path segment is null so path is empty/null
        Assert.That(sink.Entries[1].Path, Is.Null);
    }

    [Test]
    public void Scoped_UsingBlock_AutoDisposes()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var logger = LogManager.GetLogger("Test");
        LogScope scope;
        using (scope = logger.Scoped("Block"))
        {
            ((ILogger)scope).Information("inside");
        }

        Assert.That(sink.Entries[0].Path, Is.EqualTo("Block"));
    }

    [Test]
    public void Scoped_Nested_CombinesPaths()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var logger = LogManager.GetLogger("Test");
        using var outer = logger.Scoped("Service");
        using var inner = ((ILogger)outer).Scoped("Handler");
        ((ILogger)inner).Information("nested");

        Assert.That(sink.Entries[0].Path, Is.EqualTo("Service|Handler"));
    }

    [Test]
    public void Scoped_DoubleDispose_IsIdempotent()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var logger = LogManager.GetLogger("Test");
        var scope = logger.Scoped("Test");
        scope.Dispose();
        scope.Dispose(); // should not throw

        Assert.Pass();
    }

    [Test]
    public void Scoped_HasCorrectCategory()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var logger = LogManager.GetLogger("MyService");
        using var scope = logger.Scoped("Handler");
        ((ILogger)scope).Information("msg");

        Assert.That(sink.Entries[0].Category, Is.EqualTo("MyService"));
    }
}
