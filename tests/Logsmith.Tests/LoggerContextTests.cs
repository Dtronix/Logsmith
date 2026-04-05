using System.Text;
using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class LoggerContextTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void Category_ReturnsConfiguredCategory()
    {
        var ctx = new LoggerContext("MyCategory");
        Assert.That(ctx.Category, Is.EqualTo("MyCategory"));
    }

    [Test]
    public void IsEnabled_DelegatesToLogManager()
    {
        LogManager.Initialize(c => c.MinimumLevel = LogLevel.Warning);
        var ctx = new LoggerContext("Test");

        Assert.That(ctx.IsEnabled(LogLevel.Debug), Is.False);
        Assert.That(ctx.IsEnabled(LogLevel.Warning), Is.True);
    }

    [Test]
    public void IsEnabled_RespectsCategoryOverrides()
    {
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.SetMinimumLevel("Strict", LogLevel.Error);
        });

        var ctx = new LoggerContext("Strict");

        Assert.That(ctx.IsEnabled(LogLevel.Warning), Is.False);
        Assert.That(ctx.IsEnabled(LogLevel.Error), Is.True);
    }

    [Test]
    public void Dispatch_SinkReceivesDispatchInfo()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = new LoggerContext("TestCat");
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "hello"u8.ToArray(),
        };
        ctx.Dispatch(in info);

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("hello"));
        Assert.That(sink.Entries[0].Category, Is.EqualTo("TestCat"));
    }

    [Test]
    public void Dispatch_FillsTimestamp()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = new LoggerContext("Test");
        var before = DateTime.UtcNow.Ticks;
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg"u8.ToArray(),
        };
        ctx.Dispatch(in info);
        var after = DateTime.UtcNow.Ticks;

        Assert.That(sink.Entries[0].TimestampTicks, Is.InRange(before, after));
    }

    [Test]
    public void Dispatch_FillsThreadInfo()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = new LoggerContext("Test");
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg"u8.ToArray(),
        };
        ctx.Dispatch(in info);

        Assert.That(sink.Entries[0].ThreadId, Is.EqualTo(Environment.CurrentManagedThreadId));
    }

    [Test]
    public void Dispatch_NoConfig_DoesNotThrow()
    {
        // LogManager not initialized — dispatch is a no-op
        var ctx = new LoggerContext("Test");
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg"u8.ToArray(),
        };
        ctx.Dispatch(in info);
        Assert.Pass();
    }

    [Test]
    public void Dispatch_WithPathNode_IncludesPath()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = new LoggerContext("Test");
        var child = ctx.CreateChild("Handler");

        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg"u8.ToArray(),
        };
        child.Dispatch(in info);

        Assert.That(sink.Entries[0].Path, Is.EqualTo("Handler"));
    }

    [Test]
    public void Dispatch_NestedPath_IncludesFullPath()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = new LoggerContext("Test");
        var child = ctx.CreateChild("Service");
        var grandchild = child.CreateChild("Handler");

        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg"u8.ToArray(),
        };
        grandchild.Dispatch(in info);

        Assert.That(sink.Entries[0].Path, Is.EqualTo("Service|Handler"));
    }

    [Test]
    public void CreateChild_HasParentLink()
    {
        var ctx = new LoggerContext("Test");
        var child = ctx.CreateChild("child");

        Assert.That(child.Parent, Is.SameAs(ctx));
    }

    [Test]
    public void CreateChild_SharesCategory()
    {
        var ctx = new LoggerContext("MyCategory");
        var child = ctx.CreateChild("segment");

        Assert.That(child.Category, Is.EqualTo("MyCategory"));
    }

    [Test]
    public void PathSegment_GetSet()
    {
        var ctx = new LoggerContext("Test");
        var child = ctx.CreateChild("initial");

        Assert.That(child.PathSegment, Is.EqualTo("initial"));

        child.PathSegment = "changed";
        Assert.That(child.PathSegment, Is.EqualTo("changed"));
    }

    [Test]
    public void PathSegment_MutationReflectsInDispatch()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = new LoggerContext("Test");
        var child = ctx.CreateChild("before");

        child.PathSegment = "after";

        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg"u8.ToArray(),
        };
        child.Dispatch(in info);

        Assert.That(sink.Entries[0].Path, Is.EqualTo("after"));
    }

    [Test]
    public void PathCaching_RebuildsOnVersionChange()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = new LoggerContext("Test");
        var child = ctx.CreateChild("v1");

        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg1"u8.ToArray(),
        };
        child.Dispatch(in info);
        Assert.That(sink.Entries[0].Path, Is.EqualTo("v1"));

        child.PathSegment = "v2";
        var info2 = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg2"u8.ToArray(),
        };
        child.Dispatch(in info2);
        Assert.That(sink.Entries[1].Path, Is.EqualTo("v2"));
    }

    [Test]
    public void GetLogger_ReturnsSameInstanceForSameCategory()
    {
        LogManager.Initialize(c => c.AddSink(new RecordingSink()));

        var ctx1 = LogManager.GetLogger("MyCategory");
        var ctx2 = LogManager.GetLogger("MyCategory");

        Assert.That(ctx1, Is.SameAs(ctx2));
    }

    [Test]
    public void GetLogger_DifferentCategories_DifferentInstances()
    {
        LogManager.Initialize(c => c.AddSink(new RecordingSink()));

        var ctx1 = LogManager.GetLogger("Cat1");
        var ctx2 = LogManager.GetLogger("Cat2");

        Assert.That(ctx1, Is.Not.SameAs(ctx2));
    }

    [Test]
    public void GetLoggerGeneric_UsesTypeName()
    {
        LogManager.Initialize(c => c.AddSink(new RecordingSink()));

        var ctx = LogManager.GetLogger<LoggerContextTests>();

        Assert.That(ctx.Category, Is.EqualTo("LoggerContextTests"));
    }

    [Test]
    public void GetLogger_DispatchesCorrectly()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = LogManager.GetLogger("Service");
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "from context"u8.ToArray(),
        };
        ctx.Dispatch(in info);

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Category, Is.EqualTo("Service"));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("from context"));
    }

    [Test]
    public void GetLogger_RespectsReconfigure()
    {
        var sink1 = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink1));

        var ctx = LogManager.GetLogger("Service");

        var sink2 = new RecordingSink();
        LogManager.Reconfigure(c => c.AddSink(sink2));

        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "after reconfig"u8.ToArray(),
        };
        ctx.Dispatch(in info);

        // The context dispatches through LogManager which uses the new config
        Assert.That(sink2.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void RootContext_NoPathNode_PathIsNull()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        var ctx = new LoggerContext("Test");
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = "msg"u8.ToArray(),
        };
        ctx.Dispatch(in info);

        Assert.That(sink.Entries[0].Path, Is.Null);
    }

    [Test]
    public void RootContext_PathSegment_IsNull()
    {
        var ctx = new LoggerContext("Test");
        Assert.That(ctx.PathSegment, Is.Null);
    }
}
