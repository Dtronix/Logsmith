# Testing

Use `RecordingSink` to capture log entries for assertions. No mocking frameworks required.

```csharp
[TestFixture]
public class RenderLogTests
{
    private RecordingSink _sink;

    [SetUp]
    public void Setup()
    {
        _sink = new RecordingSink();
        Log.Initialize(config =>
        {
            config.MinimumLevel = LogLevel.Trace;
            config.AddSink(_sink);
        });
    }

    [Test]
    public void DrawCallCompleted_EmitsCorrectEntry()
    {
        RenderLog.DrawCallCompleted(42, 1.5);

        Assert.That(_sink.Entries, Has.Count.EqualTo(1));
        Assert.That(_sink.Entries[0].Level, Is.EqualTo(LogLevel.Debug));
        Assert.That(_sink.Entries[0].Category, Is.EqualTo("Renderer"));
        Assert.That(_sink.Entries[0].GetText(), Does.Contain("Draw call 42"));
        Assert.That(_sink.Entries[0].GetText(), Does.Contain("1.5ms"));
    }

    [Test]
    public void ShaderFailed_AttachesException()
    {
        var ex = new InvalidOperationException("compile error");
        RenderLog.ShaderFailed("MyShader", ex);

        Assert.That(_sink.Entries, Has.Count.EqualTo(1));
        Assert.That(_sink.Entries[0].Exception, Is.SameAs(ex));
        Assert.That(_sink.Entries[0].GetText(), Does.Not.Contain("InvalidOperationException"));
    }

    [TearDown]
    public void TearDown()
    {
        _sink.Dispose();
    }
}
```

## Testing with explicit sink parameter

For isolated tests that do not touch global state:

```csharp
[Test]
public void ExplicitSink_ReceivesEntry()
{
    var sink = new RecordingSink();

    // Uses the explicit sink overload, bypasses LogManager
    NetworkLog.ConnectionEstablished(sink, "10.0.0.1:8080", 12.5);

    Assert.That(sink.Entries, Has.Count.EqualTo(1));
}
```
