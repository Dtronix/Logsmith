using System.Text;
using System.Text.Json;
using Logsmith;
using Logsmith.Sample;
using Logsmith.Sinks;

var order = new OrderInfo
{
    OrderId = "ORD-101",
    Customer = "Alice",
    Total = 149.99m,
    Items = ["Widget A", "Gadget B"]
};

var config = new Dictionary<string, string>
{
    ["region"] = "us-east-1",
    ["tier"] = "premium",
    ["maxRetries"] = "3"
};

// ── 1. Text-only logging (no structured sink) ───────────────────────
Console.WriteLine("═══ Text-only logging (ConsoleSink) ═══");
Console.WriteLine();

LogManager.Initialize(c =>
{
    c.MinimumLevel = LogLevel.Trace;
    c.AddConsoleSink(colored: true);
});

Log.AppStarted(args.Length);
Log.ProcessingItem("widget", 1);
Log.CacheMiss("user:42", cachedValue: null);
Log.UserLoggedIn(1001, "Alice");
Log.UserLoggedIn(1002, displayName: null);

var reading = new SensorReading { Temperature = 23.4, Humidity = 61.2, Pressure = 1013.25 };
Log.SensorData(in reading);
Log.Checkpoint();

try { throw new InvalidOperationException("Something went wrong"); }
catch (Exception ex) { Log.OperationFailed("data-sync", ex); }

// Structured parameters still work — text path renders them via ToString / :json
Log.OrderPlaced(order);
Log.ConfigLoaded(config);
Log.OrderFlagged("ORD-101", order);

// ── 2. Add structured sink — same calls now also emit JSON ──────────
Console.WriteLine();
Console.WriteLine("═══ Text + Structured logging (ConsoleSink + JsonStructuredSink) ═══");
Console.WriteLine();

LogManager.Reconfigure(c =>
{
    c.MinimumLevel = LogLevel.Trace;
    c.AddConsoleSink(colored: true);
    c.AddSink(new JsonStructuredSink());
});

Log.AppStarted(args.Length);
Log.ProcessingItem("widget", 1);
Log.CacheMiss("user:42", cachedValue: null);
Log.UserLoggedIn(1001, "Alice");

Log.SensorData(in reading);

try { throw new InvalidOperationException("Something went wrong"); }
catch (Exception ex) { Log.OperationFailed("data-sync", ex); }

// ILogStructurable — OrderInfo controls its own JSON representation
Log.OrderPlaced(order);

// :json format specifier — Dictionary serialized as JSON in both paths
Log.ConfigLoaded(config);

// Mixed regular + structured parameters
Log.OrderFlagged("ORD-101", order);

// ── 3. Dynamic level switching ──────────────────────────────────────
Console.WriteLine();
Console.WriteLine("═══ After reconfiguring to Warning ═══");
Console.WriteLine();

LogManager.Reconfigure(c =>
{
    c.MinimumLevel = LogLevel.Warning;
    c.AddConsoleSink(colored: true);
    c.AddSink(new JsonStructuredSink());
});

// Below minimum level — skipped
Log.AppStarted(0);
Log.ProcessingItem("skipped", 0);

// Still emit
Log.CacheMiss("user:1", cachedValue: null);
Log.ShutdownCritical();

/// <summary>
/// A structured sink that writes log entries as JSON lines to the console.
/// Implements IStructuredLogSink so it receives typed properties from the generator.
/// </summary>
sealed class JsonStructuredSink : IStructuredLogSink
{
    public void Dispose() { }

    public bool IsEnabled(LogLevel level) => true;

    /// <summary>
    /// Text-only path — we skip this since WriteStructured provides richer data.
    /// </summary>
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message) { }

    /// <summary>
    /// Structured path — receives typed state and a property writer delegate.
    /// The generator creates a WriteProperties method per log call that writes
    /// each parameter as a named JSON property.
    /// </summary>
    public void WriteStructured<TState>(
        in LogEntry entry,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        writer.WriteString("level", entry.Level.ToString());
        writer.WriteString("category", entry.Category);
        writer.WriteNumber("eventId", entry.EventId);
        writer.WriteNumber("timestampTicks", entry.TimestampTicks);

        writer.WritePropertyName("properties");
        writer.WriteStartObject();
        propertyWriter(writer, state);
        writer.WriteEndObject();

        writer.WriteEndObject();
        writer.Flush();

        var json = Encoding.UTF8.GetString(stream.ToArray());
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  [JSON] ");
        Console.ResetColor();
        Console.WriteLine(json);
    }
}
