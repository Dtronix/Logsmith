# Formatting & Output

## Log Formatting

All sinks accept an `ILogFormatter` parameter that controls the prefix and suffix around each log message. Formatters write directly to `IBufferWriter<byte>` for zero-allocation output.

### DefaultLogFormatter

The default formatter produces `[HH:mm:ss.fff LVL Category] ` prefixes for console output and `[yyyy-MM-dd HH:mm:ss.fff LVL Category] ` for file output, with newline suffixes and exception rendering.

```csharp
config.AddConsoleSink(formatter: new DefaultLogFormatter(includeDate: false));
config.AddFileSink("app.log", formatter: new DefaultLogFormatter(includeDate: true));
```

### NullLogFormatter

Outputs raw messages with no prefix or suffix:

```csharp
config.AddFileSink("raw.log", formatter: NullLogFormatter.Instance);
```

### Custom formatters

Implement `ILogFormatter` for custom formatting:

```csharp
public sealed class JsonLineFormatter : ILogFormatter
{
    public void FormatPrefix(in LogEntry entry, IBufferWriter<byte> output) { /* ... */ }
    public void FormatSuffix(in LogEntry entry, IBufferWriter<byte> output) { /* ... */ }
}
```

## Format Specifiers

Message templates support format specifiers after a colon inside placeholders. Format specifiers are parsed at compile time and emitted as static code.

### Standard .NET format strings

```csharp
[LogMessage(LogLevel.Information, "Price={price:F2}, Date={date:yyyy-MM-dd}")]
public static partial void LogTransaction(decimal price, DateTime date);
// Output: "Price=19.99, Date=2026-02-18"
```

The generator emits `writer.WriteFormatted(value, "F2")` which passes the format string directly to `IUtf8SpanFormattable.TryFormat`.

### JSON serialization (`:json`)

```csharp
[LogMessage(LogLevel.Debug, "Config={config:json}")]
public static partial void LogConfig(object config);
// Output: Config={"key":"value","nested":{"a":1}}
```

The `:json` specifier uses `System.Text.Json.JsonSerializer.SerializeToUtf8Bytes` for the text path and `JsonSerializer.Serialize(writer, value)` for the structured path. Note that `:json` allocates (the `byte[]` from the serializer) — it is opt-in for complex objects.

The generator emits `LSMITH006` warning when `:json` is applied to primitive types (`int`, `string`, `bool`, etc.) where default formatting is more efficient.

## Structured Output

Every log method generates two output paths. Text sinks receive a pre-formatted UTF8 byte span. Structured sinks receive typed property writes through `System.Text.Json.Utf8JsonWriter`.

A structured sink (such as a JSON file sink or a network sink) implements `IStructuredLogSink`:

```csharp
public interface IStructuredLogSink : ILogSink
{
    void WriteStructured<TState>(
        in LogEntry entry,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct;
}
```

The generator emits a static lambda for each log method that writes properties without closure allocations:

```csharp
// Generated for: DrawCallCompleted(int drawCallId, double elapsedMs)
static (writer, state) =>
{
    writer.WriteNumber("drawCallId"u8, state.drawCallId);
    writer.WriteNumber("elapsedMs"u8, state.elapsedMs);
}
```

The property names are UTF8 string literals derived from the parameter names at compile time.
