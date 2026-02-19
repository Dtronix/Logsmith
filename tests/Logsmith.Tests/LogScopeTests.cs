using System.Text;
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
    public void Push_SingleProperty_AppearsInEnumeration()
    {
        using var scope = LogScope.Push("RequestId", "abc-123");

        var props = CollectProperties();
        Assert.That(props, Has.Count.EqualTo(1));
        Assert.That(props[0].Key, Is.EqualTo("RequestId"));
        Assert.That(props[0].Value, Is.EqualTo("abc-123"));
    }

    [Test]
    public void Push_MultipleProperties_AllAppear()
    {
        var kvps = new KeyValuePair<string, string>[]
        {
            new("A", "1"),
            new("B", "2"),
        };
        using var scope = LogScope.Push(kvps);

        var props = CollectProperties();
        Assert.That(props, Has.Count.EqualTo(2));
    }

    [Test]
    public void Dispose_RestoresParent()
    {
        using (LogScope.Push("outer", "1"))
        {
            using (LogScope.Push("inner", "2"))
            {
                Assert.That(CollectProperties(), Has.Count.EqualTo(2));
            }

            var after = CollectProperties();
            Assert.That(after, Has.Count.EqualTo(1));
            Assert.That(after[0].Key, Is.EqualTo("outer"));
        }

        Assert.That(LogScope.Current, Is.Null);
    }

    [Test]
    public void Dispatch_TextSink_IncludesScopeProperties()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        using var scope = LogScope.Push("TraceId", "xyz");
        DispatchTestMessage(LogLevel.Information, "hello");

        Assert.That(sink.Entries[0].Message, Does.Contain("[TraceId=xyz]"));
    }

    [Test]
    public void Dispatch_NoScope_MessageUnchanged()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        DispatchTestMessage(LogLevel.Information, "plain");

        Assert.That(sink.Entries[0].Message, Is.EqualTo("plain"));
    }

    [Test]
    public void Scope_FlowsThroughAsyncLocal()
    {
        List<KeyValuePair<string, string>>? innerProps = null;

        using var scope = LogScope.Push("key", "val");
        var task = Task.Run(() =>
        {
            innerProps = CollectProperties();
        });
        task.Wait();

        Assert.That(innerProps, Has.Count.EqualTo(1));
        Assert.That(innerProps![0].Key, Is.EqualTo("key"));
    }

    private static List<KeyValuePair<string, string>> CollectProperties()
    {
        var list = new List<KeyValuePair<string, string>>();
        var enumerator = LogScope.EnumerateProperties();
        while (enumerator.MoveNext())
        {
            list.Add(enumerator.Current);
        }
        return list;
    }

    private static void DispatchTestMessage(LogLevel level, string message, string category = "Test")
    {
        if (!LogManager.IsEnabled(level, category))
            return;

        var entry = new LogEntry(
            level: level,
            eventId: 1,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: category);

        var utf8 = Encoding.UTF8.GetBytes(message).AsSpan();
        LogManager.Dispatch(in entry, utf8, 0, static (writer, state) => { });
    }
}
