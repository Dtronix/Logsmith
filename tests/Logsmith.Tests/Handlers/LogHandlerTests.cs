using System.Text.Json;
using Logsmith.Handlers;
using Logsmith.Sinks;

namespace Logsmith.Tests.Handlers;

[TestFixture]
public class LogHandlerTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    private static ILogger CreateLogger(RecordingSink sink, LogLevel minLevel = LogLevel.Trace)
    {
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = minLevel;
            c.AddSink(sink);
        });
        return LogManager.GetLogger("Test");
    }

    // ── Dual-buffer output tests ────────────────────────────────────────

    [Test]
    public void Debug_InterpolatedString_ProducesTextAndJson()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var userId = 42;
        var name = "Alice";

        logger.Debug($"User {userId} named {name}");

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("User 42 named Alice"));
        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Debug));

        // Verify JSON output
        Assert.That(sink.Entries[0].JsonMessage, Is.Not.Null);
        var json = JsonDocument.Parse(sink.Entries[0].JsonMessage!);
        Assert.That(json.RootElement.GetProperty("userId").GetInt32(), Is.EqualTo(42));
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("Alice"));
    }

    [Test]
    public void Information_InterpolatedString_ProducesTextAndJson()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var count = 5;

        logger.Information($"Processed {count} items");

        Assert.That(sink.Entries[0].Message, Is.EqualTo("Processed 5 items"));
        var json = JsonDocument.Parse(sink.Entries[0].JsonMessage!);
        Assert.That(json.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(5));
    }

    [Test]
    public void Warning_InterpolatedString_CorrectLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Warning($"Slow query took {150}ms");

        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Warning));
    }

    [Test]
    public void Error_InterpolatedString_CorrectLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Error($"Failed to connect");

        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Error));
    }

    [Test]
    public void Critical_InterpolatedString_CorrectLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Critical($"System shutdown");

        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Critical));
    }

    [Test]
    public void Trace_InterpolatedString_CorrectLevel()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Trace($"Entering method");

        Assert.That(sink.Entries[0].Level, Is.EqualTo(LogLevel.Trace));
    }

    // ── Short-circuit tests ─────────────────────────────────────────────

    [Test]
    public void Debug_Disabled_DoesNotDispatch()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink, LogLevel.Warning);

        logger.Debug($"User {42} should not appear");

        Assert.That(sink.Entries, Is.Empty);
    }

    [Test]
    public void Trace_Disabled_DoesNotDispatch()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink, LogLevel.Information);

        logger.Trace($"Should be filtered");

        Assert.That(sink.Entries, Is.Empty);
    }

    // ── Exception handling tests ────────────────────────────────────────

    [Test]
    public void Error_WithException_IncludesException()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var ex = new InvalidOperationException("boom");
        var code = 500;

        logger.Error(ex, $"Request failed with {code}");

        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("Request failed with 500"));
    }

    [Test]
    public void Debug_WithException_IncludesException()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var ex = new ArgumentException("bad");

        logger.Debug(ex, $"Diagnostics for issue");

        Assert.That(sink.Entries[0].Exception, Is.SameAs(ex));
    }

    // ── JSON property name tests (CallerArgumentExpression) ─────────────

    [Test]
    public void CallerArgumentExpression_CapturesVariableName()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var orderId = "ORD-123";
        var total = 99.99;

        logger.Information($"Order {orderId} total {total}");

        var json = JsonDocument.Parse(sink.Entries[0].JsonMessage!);
        Assert.That(json.RootElement.GetProperty("orderId").GetString(), Is.EqualTo("ORD-123"));
        Assert.That(json.RootElement.GetProperty("total").GetDouble(), Is.EqualTo(99.99));
    }

    // ── JIT-specialized type tests ──────────────────────────────────────

    [Test]
    public void Json_IntValue_WrittenAsNumber()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var count = 42;

        logger.Debug($"Count: {count}");

        var json = JsonDocument.Parse(sink.Entries[0].JsonMessage!);
        Assert.That(json.RootElement.GetProperty("count").ValueKind, Is.EqualTo(JsonValueKind.Number));
        Assert.That(json.RootElement.GetProperty("count").GetInt32(), Is.EqualTo(42));
    }

    [Test]
    public void Json_BoolValue_WrittenAsBool()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var active = true;

        logger.Debug($"Active: {active}");

        var json = JsonDocument.Parse(sink.Entries[0].JsonMessage!);
        Assert.That(json.RootElement.GetProperty("active").ValueKind, Is.EqualTo(JsonValueKind.True));
    }

    [Test]
    public void Json_DoubleValue_WrittenAsNumber()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var elapsed = 3.14;

        logger.Debug($"Elapsed: {elapsed}");

        var json = JsonDocument.Parse(sink.Entries[0].JsonMessage!);
        Assert.That(json.RootElement.GetProperty("elapsed").GetDouble(), Is.EqualTo(3.14));
    }

    [Test]
    public void Json_StringValue_WrittenAsString()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var name = "test";

        logger.Debug($"Name: {name}");

        var json = JsonDocument.Parse(sink.Entries[0].JsonMessage!);
        Assert.That(json.RootElement.GetProperty("name").GetString(), Is.EqualTo("test"));
    }

    [Test]
    public void Json_GuidValue_WrittenAsString()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        logger.Debug($"Id: {id}");

        var json = JsonDocument.Parse(sink.Entries[0].JsonMessage!);
        Assert.That(json.RootElement.GetProperty("id").GetString(), Is.EqualTo("12345678-1234-1234-1234-123456789abc"));
    }

    [Test]
    public void Json_NullValue_WrittenAsNull()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        string? value = null;

        logger.Debug($"Value: {value}");

        Assert.That(sink.Entries[0].Message, Is.EqualTo("Value: null"));
    }

    // ── Format specifier tests ──────────────────────────────────────────

    [Test]
    public void FormatSpecifier_AppliedToText()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);
        var price = 42.567;

        logger.Debug($"Price: {price:F2}");

        Assert.That(sink.Entries[0].Message, Is.EqualTo("Price: 42.57"));
    }

    // ── GetJsonWritten idempotency ──────────────────────────────────────

    [Test]
    public void Handler_GetJsonWritten_IdempotentOnSecondCall()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        // Manually test handler core
        var core = new LogHandlerCore(10, 1, logger, LogLevel.Debug, out var isEnabled);
        Assert.That(isEnabled, Is.True);

        core.AppendLiteral("Count: ");
        core.AppendFormatted(5, "count");

        var json1 = core.GetJsonWritten();
        var json2 = core.GetJsonWritten();

        Assert.That(json1.Length, Is.EqualTo(json2.Length));
    }

    // ── Literal-only interpolation (no formatted args) ──────────────────

    [Test]
    public void LiteralOnly_NoJsonOutput()
    {
        var sink = new RecordingSink();
        var logger = CreateLogger(sink);

        logger.Debug($"No parameters here");

        Assert.That(sink.Entries[0].Message, Is.EqualTo("No parameters here"));
        // No formatted values → no JSON
        Assert.That(sink.Entries[0].JsonMessage, Is.Null);
    }

    // ── End-to-end dispatch ─────────────────────────────────────────────

    [Test]
    public void EndToEnd_DispatchesThroughLoggerContext()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("MyService");
        var userId = 42;
        var action = "login";

        logger.Information($"User {userId} performed {action}");

        Assert.That(sink.Entries[0].Category, Is.EqualTo("MyService"));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("User 42 performed login"));
        Assert.That(sink.Entries[0].TimestampTicks, Is.GreaterThan(0));
    }

    [Test]
    public void EndToEnd_WithPath_IncludesPath()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Trace;
            c.AddSink(sink);
        });

        var logger = LogManager.GetLogger("Service");
        var child = logger.CreateChild("Handler");
        var count = 3;

        child.Debug($"Processing {count} items");

        Assert.That(sink.Entries[0].Path, Is.EqualTo("Handler"));
        Assert.That(sink.Entries[0].Message, Is.EqualTo("Processing 3 items"));
    }
}
