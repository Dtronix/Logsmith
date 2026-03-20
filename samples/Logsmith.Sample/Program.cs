using System.Text;
using System.Text.Json;
using Logsmith;
using Logsmith.Sample;
using Logsmith.Sinks;

// Initialize with console sink + a structured JSON sink
LogManager.Initialize(c =>
{
    c.MinimumLevel = LogLevel.Trace;
    c.AddConsoleSink(colored: true);
    c.AddSink(new JsonStructuredSink());
});

// Various log levels and parameter types
Log.AppStarted(args.Length);
Log.ProcessingItem("widget", 1);
Log.CacheMiss("user:42", cachedValue: null);
Log.CacheMiss("user:99", cachedValue: 7);
Log.UserLoggedIn(1001, "Alice");
Log.UserLoggedIn(1002, displayName: null);

// Large struct passed by reference — no copy at the call site
var reading = new SensorReading { Temperature = 23.4, Humidity = 61.2, Pressure = 1013.25 };
Log.SensorData(in reading);

// Caller info — file, line, and member auto-populated
Log.Checkpoint();

// Exception logging
try
{
    throw new InvalidOperationException("Something went wrong");
}
catch (Exception ex)
{
    Log.OperationFailed("data-sync", ex);
}

// ── Structured logging ───────────────────────────────────────────────
Console.WriteLine();
Console.WriteLine("── Structured logging ──");
Console.WriteLine();

// ILogStructurable — the OrderInfo type controls its own JSON serialization
var order = new OrderInfo
{
    OrderId = "ORD-101",
    Customer = "Alice",
    Total = 149.99m,
    Items = ["Widget A", "Gadget B"]
};
Log.OrderPlaced(order);

// :json format specifier — Dictionary serialized inline as JSON
var config = new Dictionary<string, string>
{
    ["region"] = "us-east-1",
    ["tier"] = "premium",
    ["maxRetries"] = "3"
};
Log.ConfigLoaded(config);

// Mixed regular + structured parameters
Log.OrderFlagged("ORD-101", order);

// Reconfigure to raise minimum level
LogManager.Reconfigure(c =>
{
    c.MinimumLevel = LogLevel.Warning;
    c.AddConsoleSink(colored: true);
    c.AddSink(new JsonStructuredSink());
});

Console.WriteLine();
Console.WriteLine("--- After reconfiguring to Warning ---");
Console.WriteLine();

// These are now below minimum level and will be skipped
Log.AppStarted(0);
Log.ProcessingItem("skipped", 0);

// These still emit
Log.CacheMiss("user:1", cachedValue: null);
Log.ShutdownCritical();

/// <summary>
/// A structured sink that writes log entries as JSON lines (JSONL) to the console.
/// Implements IStructuredLogSink so it receives typed properties from the generator.
/// </summary>
sealed class JsonStructuredSink : IStructuredLogSink
{
    public void Dispose() { }

    public bool IsEnabled(LogLevel level) => true;

    /// <summary>
    /// Text-only path — receives the pre-formatted UTF-8 message.
    /// We skip this since WriteStructured provides richer data.
    /// </summary>
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // No-op: we only handle structured output via WriteStructured.
    }

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

        // Write the generator-produced properties — each log parameter
        // is written as a named JSON property by the generated delegate
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
