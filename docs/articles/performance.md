# Performance

## `in` Parameters

For large value types, use the `in` modifier to pass by reference and avoid copying. The generator preserves `in` through the entire pipeline: method signature, state struct constructor, and state construction.

```csharp
public struct SensorReading : IUtf8SpanFormattable
{
    public double Temperature, Humidity, Pressure;
    // ...
}

[LogCategory("Sensors")]
public static partial class SensorLog
{
    [LogMessage(LogLevel.Information, "Sensor reported {reading}")]
    public static partial void SensorData(in SensorReading reading);
}

// At the call site, the struct is passed by reference — no copy
SensorLog.SensorData(in reading);
```

Without `in`, the struct would be copied at each handoff (call site to method, method to state constructor). With `in`, only a single copy occurs when the value is stored into the state struct field, which is unavoidable since references cannot be stored in fields.

The `in` modifier is transparent at the call site for value types — existing callers that don't specify `in` explicitly continue to work (the compiler passes by reference automatically).

## Custom Type Serialization

### Text output

The generator selects the optimal formatting strategy for each parameter type at compile time, in this priority order:

1. `IUtf8SpanFormattable` -- direct UTF8 write to `Span<byte>`, zero allocation.
2. `ISpanFormattable` -- write to stack-allocated `Span<char>`, transcode to UTF8.
3. `IFormattable` -- calls `ToString(null, null)`.
4. `ToString()` -- last resort.

For optimal performance, implement `IUtf8SpanFormattable` on your types:

```csharp
public struct Mat3 : IUtf8SpanFormattable
{
    public float M00, M01, M02, M10, M11, M12, M20, M21, M22;

    public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten,
        ReadOnlySpan<char> format, IFormatProvider? provider)
    {
        // Write directly to UTF8 buffer, no intermediate strings
    }
}
```

### Structured output

For the JSON property path, implement `ILogStructurable`:

```csharp
public interface ILogStructurable
{
    void WriteStructured(Utf8JsonWriter writer);
}

public struct Mat3 : IUtf8SpanFormattable, ILogStructurable
{
    public void WriteStructured(Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(M00);
        writer.WriteNumberValue(M01);
        // ... remaining values
        writer.WriteEndArray();
    }
}
```

The generator detects these interfaces at compile time and emits the appropriate call. No runtime type checks.

## Nullable Parameters

The generator handles nullable types with compile-time null guards:

```csharp
[LogMessage(LogLevel.Debug, "Result: {value}, User: {userName}")]
public static partial void LogResult(int? value, string? userName);
```

For nullable value types (`int?`, `double?`), the generator emits a `HasValue` check and writes `"null"` as a UTF8 literal when empty. For nullable reference types, it emits a null reference check. The structured path uses `Utf8JsonWriter.WriteNull()` for null values.

## Caller Information

Add `[CallerFilePath]`, `[CallerLineNumber]`, or `[CallerMemberName]` parameters in any order and in any combination. The generator identifies them by attribute, not by position, and excludes them from message template matching.

```csharp
[LogMessage(LogLevel.Error, "Operation failed: {reason}")]
public static partial void OperationFailed(
    string reason,
    Exception ex,
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0,
    [CallerMemberName] string member = "");
```

The C# compiler fills these in at each call site with interned string literals and integer constants. No runtime cost. The values are attached to `LogEntry` for sinks to include in their output.

Caller parameters can appear before, after, or interleaved with message parameters:

```csharp
// All valid
public static partial void Foo(int x, [CallerLineNumber] int line = 0);
public static partial void Foo([CallerMemberName] string member = "", int x = 0);
public static partial void Foo([CallerFilePath] string file = "", int x = 0, [CallerLineNumber] int line = 0);
```

## Exception Handling

If a parameter's type is `Exception` (or a derived type), the generator treats it as a special attachment rather than a message template value. It is stored on `LogEntry.Exception` and not interpolated into the text output.

```csharp
[LogMessage(LogLevel.Error, "Request to {endpoint} failed with status {statusCode}")]
public static partial void RequestFailed(string endpoint, int statusCode, Exception ex);
// Text output: "Request to /api/users failed with status 500"
// Exception attached separately in LogEntry.Exception for sinks to render
```

## Explicit Sink Parameter

By default, log methods dispatch through the global `LogManager`. To route to a specific sink, add an `ILogSink` parameter. The generator detects it by type and uses it directly instead of the global dispatch.

```csharp
[LogMessage(LogLevel.Debug, "Test event: {value}")]
public static partial void TestEvent(ILogSink sink, int value);
```

This is useful for testing with a `RecordingSink` or for routing specific log paths to dedicated sinks without configuring category filters.

## Benchmarks

See the [benchmarks project](https://github.com/Dtronix/Logsmith/tree/master/benchmarks/Logsmith.Benchmarks) for comparative measurements against other logging frameworks.
