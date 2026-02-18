<p align="center">
  <img src="logo.png" alt="Logsmith" width="120" />
</p>

<h1 align="center">Logsmith</h1>

<p align="center"><strong>Zero-allocation, source-generated structured logging for .NET 10.</strong></p>

Logsmith is a logging framework where the source generator *is* the framework. Every log method is analyzed at compile time, and the generator emits fully specialized, zero-allocation UTF8 code tailored to your exact parameters. No reflection. No boxing. No runtime parsing of message templates. No middleware. Just direct, type-safe writes from your call site to your output sink.

---

## Table of Contents

- [Packages](#packages)
- [Why Logsmith Exists](#why-logsmith-exists)
- [What Logsmith Is](#what-logsmith-is)
- [What Logsmith Is Not](#what-logsmith-is-not)
- [Comparison with Other Frameworks](#comparison-with-other-frameworks)
- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Declaring Log Methods](#declaring-log-methods)
- [Message Templates](#message-templates)
- [Log Levels and Conditional Compilation](#log-levels-and-conditional-compilation)
- [Sinks](#sinks)
- [Log Formatting](#log-formatting)
- [Format Specifiers](#format-specifiers)
- [Structured Output](#structured-output)
- [Performance: `in` Parameters](#performance-in-parameters)
- [Custom Type Serialization](#custom-type-serialization)
- [Nullable Parameters](#nullable-parameters)
- [Caller Information](#caller-information)
- [Exception Handling](#exception-handling)
- [Explicit Sink Parameter](#explicit-sink-parameter)
- [Multi-Project Solutions](#multi-project-solutions)
- [Compile-Time Diagnostics](#compile-time-diagnostics)
- [Extending Logsmith](#extending-logsmith)
- [Testing](#testing)
- [Configuration Reference](#configuration-reference)
- [Architecture](#architecture)
- [License](#license)

## Packages

| Name | NuGet | Description |
|------|-------|-------------|
| [`Logsmith`](https://www.nuget.org/packages/Logsmith) | [![Logsmith](https://img.shields.io/nuget/v/Logsmith.svg?maxAge=60)](https://www.nuget.org/packages/Logsmith) | Runtime library with public types, sinks, and the bundled source generator. Use this when multiple projects share log definitions. |
| [`Logsmith.Generator`](https://www.nuget.org/packages/Logsmith.Generator) | [![Logsmith.Generator](https://img.shields.io/nuget/v/Logsmith.Generator.svg?maxAge=60)](https://www.nuget.org/packages/Logsmith.Generator) | Source generator only. Emits all infrastructure as internal types with zero runtime dependency. |

---

## Why Logsmith Exists

Most .NET logging frameworks share a common design: a runtime library parses message templates, boxes value-type arguments into `object[]`, and dispatches through multiple abstraction layers before bytes ever reach an output target. This design prioritizes plugin ecosystems and runtime flexibility over raw throughput.

For applications where logging sits on the hot path, those costs are measurable. Game engines evaluating draw calls at 144 frames per second. Trading systems processing market data where microseconds matter. Libraries that want structured logging without imposing transitive dependencies on their consumers. NativeAOT deployments where reflection is unavailable and binary size matters.

The `Microsoft.Extensions.Logging` `LoggerMessage` source generator demonstrated that compile-time code generation could eliminate much of this overhead. Logsmith takes that idea to its conclusion: the source generator does not supplement a runtime framework. It replaces it entirely.

The generator reads your method declarations at build time. It knows the concrete types of every parameter. It emits direct UTF8 formatting calls, pre-computed property names, and type-specific serialization paths. In standalone mode, the consuming project's compiled output contains zero Logsmith DLLs. Everything is source-generated into your assembly.

---

## What Logsmith Is

- A C# incremental source generator that emits fully specialized logging method bodies at compile time.
- A zero-allocation logging pipeline that stays in UTF8 from input to output.
- A structured logging system that captures typed properties alongside human-readable messages.
- A dual-mode package: reference the runtime library for shared types across projects, or use the generator alone for fully self-contained internal logging with no runtime dependency.
- A compile-time safety net with diagnostics that catch template mismatches, missing parameters, and unsupported types before your code ever runs.
- A framework that supports `in` parameters for passing large structs by reference, eliminating unnecessary copies on the logging hot path.
- A framework that uses `[Conditional("DEBUG")]` to strip debug and trace log calls from release binaries at the compiler level, not just filter them at runtime.

## What Logsmith Is Not

- **Not a replacement for `Microsoft.Extensions.Logging` in the ASP.NET Core ecosystem.** If your application depends on MEL-compatible sinks like Seq, Datadog, Application Insights, or OpenTelemetry collectors, those integrations expect MEL's `ILogger` interface. Logsmith defines its own `ILogSink` contract.
- **Not a runtime-configurable logging framework.** Log levels can be changed at runtime, and sinks can be reconfigured, but message templates and parameter bindings are fixed at compile time. There is no runtime expression evaluator or dynamic template engine.

---

## Comparison with Other Frameworks

| Capability | Logsmith | MEL + LoggerMessage | ZLogger | Serilog | NLog |
|---|---|---|---|---|---|
| Source-generated method bodies | Yes | Yes | Yes | No | No |
| Zero runtime dependency mode | Yes (standalone) | No (requires MEL) | No (requires MEL) | No | No |
| Zero allocation hot path | Yes | Partial (MEL infra allocates) | Yes | No | No |
| UTF8 end-to-end | Yes | No (UTF16 strings) | Yes | No | No |
| Structured logging | Yes (Utf8JsonWriter) | Yes | Yes | Yes | Yes |
| Compile-time level stripping | Yes ([Conditional]) | No | No | No | No |
| No boxing of value types | Yes | Yes (generated path) | Yes | No (object[] params) | No (object[] params) |
| No reflection | Yes | Yes | Partial | No (used in enrichers) | No (used in layouts) |
| NativeAOT compatible | Yes | Yes | Yes | Partial | Partial |
| Compile-time diagnostics | Yes (LSMITH001-006) | Yes | Limited | No | No |
| Custom type serialization | ILogStructurable | ILogger.BeginScope | IZLoggerFormattable | Destructure policies | Custom layout renderers |
| MEL ecosystem compatibility | No | Native | Native | Via Serilog.Extensions.Logging | Via NLog.Extensions.Logging |
| DI container required | No | Typically yes | Typically yes | No | No |
| Transitive dependencies | Zero (standalone) | MEL abstractions | MEL + ZLogger | Serilog + sinks | NLog |

---

## Features

### Zero-Allocation Logging Pipeline

The generator emits direct calls to `IUtf8SpanFormattable.TryFormat` for every value-type parameter, writing UTF8 bytes to stack-allocated buffers. No `ToString()`. No intermediate strings. No heap allocations on the logging hot path.

### Compile-Time Conditional Level Stripping

Log methods at or below a configurable severity threshold receive `[Conditional("DEBUG")]`, causing the C# compiler to erase call sites entirely from release builds. The method body, the argument evaluation, and the call itself are absent from the compiled IL.

### Dual-Mode Packaging

Reference the `Logsmith` NuGet package for shared public types and the bundled generator. Or reference `Logsmith.Generator` alone, and the generator emits all infrastructure as internal types. The generator detects which mode applies automatically.

### Structured and Text Output

Every log method generates two output paths: a human-readable UTF8 text message and a structured property set written through `System.Text.Json.Utf8JsonWriter`. Sinks choose which representation they consume.

### Compile-Time Validation

The generator produces diagnostics for template placeholder mismatches, unreferenced parameters, invalid method signatures, and unsupported parameter types. Errors surface in the IDE before the code compiles.

### Built-In Sinks

Six sinks ship with the framework: `ConsoleSink` with ANSI color, `FileSink` with async-buffered writing, size and time-based rolling, and multi-process support, `StreamSink` for writing to any `Stream`, `DebugSink` for IDE output windows, `RecordingSink` for test assertions, and `NullSink` for benchmarking.

### Pluggable Log Formatting

All sinks accept an `ILogFormatter` for customizing log line prefixes and suffixes. `DefaultLogFormatter` provides timestamp, level, and category formatting. `NullLogFormatter` outputs raw messages only. Custom formatters write directly to `IBufferWriter<byte>` for zero-allocation output.

### Format Specifiers in Templates

Message templates support standard .NET format strings (`{value:F2}`) and JSON serialization (`{obj:json}`). Format specifiers are parsed at compile time and emitted as static code — no runtime template parsing.

### Internal Error Handling

Sink exceptions in `LogManager.Dispatch` are caught and routed to a configurable `InternalErrorHandler`. A failed sink does not prevent other sinks from executing or crash the application.

### Thread Info Capture

Every `LogEntry` carries `ThreadId` and `ThreadName` captured at the call site. Available for structured sinks and explicit template references (`{threadId}`, `{threadName}`). Not rendered in text output by default.

---

## Installation

### Standard (recommended for most projects)

```xml
<PackageReference Include="Logsmith" Version="1.0.0" />
```

This provides the runtime library (public types, sinks, `LogManager`) and the source generator. The generator is bundled as an analyzer and does not appear in your build output.

### Standalone (zero runtime dependency)

```xml
<PackageReference Include="Logsmith.Generator" Version="1.0.0"
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false" />
```

The generator emits all infrastructure types as `internal` into your assembly. No Logsmith DLLs appear in your build output.

---

## Quick Start

### 1. Initialize at startup

```csharp
LogManager.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Debug;
    config.AddConsoleSink();
    config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
    config.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
});
```

### 2. Declare log methods

```csharp
[LogCategory("Renderer")]
public static partial class RenderLog
{
    [LogMessage(LogLevel.Debug, "Draw call {drawCallId} completed in {elapsedMs}ms")]
    public static partial void DrawCallCompleted(int drawCallId, double elapsedMs);

    [LogMessage(LogLevel.Error, "Shader compilation failed: {shaderName}")]
    public static partial void ShaderFailed(string shaderName, Exception ex);
}
```

### 3. Call them

```csharp
public class Renderer
{
    public void Draw(int id)
    {
        var sw = Stopwatch.StartNew();
        // ... rendering work ...
        sw.Stop();

        RenderLog.DrawCallCompleted(id, sw.Elapsed.TotalMilliseconds);
    }
}
```

No logger injection. No sink parameter. No service locator. The generated code dispatches through the static `LogManager` configured at startup.

---

## Declaring Log Methods

Log methods are declared as `static partial` methods inside `partial` classes. The generator provides the implementation.

```csharp
public static partial class NetworkLog
{
    [LogMessage(LogLevel.Information, "Connection established to {endpoint} in {latencyMs}ms")]
    public static partial void ConnectionEstablished(string endpoint, double latencyMs);

    [LogMessage(LogLevel.Warning, "Packet loss detected: {lossPercent}% over {windowSeconds}s")]
    public static partial void PacketLoss(float lossPercent, int windowSeconds);

    [LogMessage(LogLevel.Critical, "Connection to {endpoint} lost")]
    public static partial void ConnectionLost(string endpoint, Exception ex);
}
```

Requirements:
- The containing class must be `partial`.
- The method must be `static partial`.
- The method must return `void`.
- Parameter names referenced in the message template are matched case-insensitively.
- Parameters may use the `in` modifier to pass large structs by reference (see [Performance: `in` Parameters](#performance-in-parameters)).

### Categories

The `[LogCategory]` attribute sets the category string attached to every log entry from that class. If omitted, the class name is used.

```csharp
[LogCategory("Audio")]
public static partial class AudioLog { ... }

// No attribute: category defaults to "PhysicsLog"
public static partial class PhysicsLog { ... }
```

---

## Message Templates

### Explicit templates

Provide a message string with `{parameterName}` placeholders that map to method parameters by name (case-insensitive):

```csharp
[LogMessage(LogLevel.Debug, "Frame {frameId} rendered {triangleCount} triangles in {elapsedMs}ms")]
public static partial void FrameRendered(int frameId, long triangleCount, double elapsedMs);
```

The generator pre-splits the template at compile time into alternating literal segments and parameter slots. At runtime, it writes UTF8 literals directly and formats each parameter through its `IUtf8SpanFormattable` implementation.

### Template-free mode

Omit the message string. The generator constructs the message automatically from the method name and parameter names:

```csharp
[LogMessage(LogLevel.Debug)]
public static partial void FrameRendered(int frameId, long triangleCount, double elapsedMs);
// Generated message: "FrameRendered frameId={frameId} triangleCount={triangleCount} elapsedMs={elapsedMs}"
```

This mode guarantees that renaming a parameter via IDE refactoring keeps the message in sync. The method name is split from PascalCase for the structured event name.

### EventId

Each log method receives a stable `EventId` derived from a hash of the fully qualified method name. To override:

```csharp
[LogMessage(LogLevel.Information, "Player joined: {playerName}", EventId = 5001)]
public static partial void PlayerJoined(string playerName);
```

---

## Log Levels and Conditional Compilation

### Log levels

```csharp
public enum LogLevel : byte
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    None
}
```

### Runtime filtering

`LogManager` performs an enum comparison before dispatching. If the entry's level is below the configured minimum, no work is done:

```csharp
// Fast path: single enum comparison, no allocations
if (level < _config.MinimumLevel) return;
```

Per-category overrides are supported:

```csharp
Log.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Information;
    config.SetMinimumLevel("Renderer", LogLevel.Debug);
});
```

### Compile-time stripping

The generator applies `[Conditional("DEBUG")]` to log methods at or below a configurable severity threshold. The C# compiler erases these call sites entirely from release builds. No IL is emitted. Arguments are not evaluated.

Configure the threshold in your project file:

```xml
<PropertyGroup>
    <!-- Default: Debug. Methods at Trace and Debug get [Conditional("DEBUG")] -->
    <LogsmithConditionalLevel>Debug</LogsmithConditionalLevel>
</PropertyGroup>
```

| Setting | Methods stripped in Release |
|---|---|
| `Trace` | Trace only |
| `Debug` (default) | Trace, Debug |
| `Information` | Trace, Debug, Information |
| `None` | Nothing stripped |

To exempt a specific method from stripping regardless of the threshold:

```csharp
[LogMessage(LogLevel.Debug, "Critical diagnostic: {value}", AlwaysEmit = true)]
public static partial void CriticalDiagnostic(double value);
```

---

## Sinks

### Built-in sinks

#### ConsoleSink

Writes ANSI-colored UTF8 directly to `Console.OpenStandardOutput()`, bypassing `Console.WriteLine` and its encoding overhead.

```csharp
config.AddConsoleSink();
config.AddConsoleSink(colored: false); // disable ANSI colors
```

Output format:
```
[12:34:56 DBG] Renderer: Draw call 42 completed in 1.3ms
[12:34:56 INF] Renderer: Frame rendered 100 with 50000 triangles
[12:34:56 WRN] Audio: Buffer underrun detected
[12:34:56 ERR] Network: Connection to 10.0.0.1:8080 lost
```

#### FileSink

Async-buffered file writing using `Channel<T>`. The calling thread enqueues a buffered copy and returns immediately. A background task flushes to disk.

```csharp
config.AddFileSink("logs/app.log");
config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Hourly, maxFileSizeBytes: 50_000_000);
config.AddFileSink("logs/app.log", shared: true); // multi-process safe
```

Rolling intervals: `None`, `Hourly`, `Daily`, `Weekly`, `Monthly`. Size-based rolling and time-based rolling can be combined. In shared mode (`shared: true`), the file is opened with `FileShare.ReadWrite` for safe concurrent appends from multiple processes.

#### StreamSink

Writes to any `Stream` via async-buffered `Channel<T>`. Useful for network streams, memory streams, or `Console.OpenStandardOutput()`.

```csharp
config.AddStreamSink(networkStream, leaveOpen: true);
```

When `leaveOpen` is true, the stream is flushed but not disposed when the sink is disposed.

#### DebugSink

Writes to `System.Diagnostics.Debug`, which routes to the IDE output window. Useful during development. Automatically stripped from release builds by the runtime.

```csharp
config.AddDebugSink();
```

#### RecordingSink

Captures log entries to an in-memory list for test assertions. See [Testing](#testing).

```csharp
var sink = new RecordingSink();
config.AddSink(sink);
```

#### NullSink

Discards all output. Useful for benchmarking the logging pipeline itself or for disabling logging without removing call sites.

```csharp
config.AddNullSink();
```

### Sink filtering by category

Any sink can be restricted to specific categories:

```csharp
config.AddFileSink("logs/render.log", category: "Renderer");
config.AddFileSink("logs/network.log", category: "Network");
config.AddConsoleSink(); // receives everything
```

---

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

---

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

---

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

---

## Performance: `in` Parameters

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

---

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

---

## Nullable Parameters

The generator handles nullable types with compile-time null guards:

```csharp
[LogMessage(LogLevel.Debug, "Result: {value}, User: {userName}")]
public static partial void LogResult(int? value, string? userName);
```

For nullable value types (`int?`, `double?`), the generator emits a `HasValue` check and writes `"null"` as a UTF8 literal when empty. For nullable reference types, it emits a null reference check. The structured path uses `Utf8JsonWriter.WriteNull()` for null values.

---

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

---

## Exception Handling

If a parameter's type is `Exception` (or a derived type), the generator treats it as a special attachment rather than a message template value. It is stored on `LogEntry.Exception` and not interpolated into the text output.

```csharp
[LogMessage(LogLevel.Error, "Request to {endpoint} failed with status {statusCode}")]
public static partial void RequestFailed(string endpoint, int statusCode, Exception ex);
// Text output: "Request to /api/users failed with status 500"
// Exception attached separately in LogEntry.Exception for sinks to render
```

---

## Explicit Sink Parameter

By default, log methods dispatch through the global `LogManager`. To route to a specific sink, add an `ILogSink` parameter. The generator detects it by type and uses it directly instead of the global dispatch.

```csharp
[LogMessage(LogLevel.Debug, "Test event: {value}")]
public static partial void TestEvent(ILogSink sink, int value);
```

This is useful for testing with a `RecordingSink` or for routing specific log paths to dedicated sinks without configuring category filters.

---

## Multi-Project Solutions

### Single project or standalone application

Reference `Logsmith` or `Logsmith.Generator` alone. All types are available within the project, either as public types from the runtime library or as generated internal types.

### Multiple projects sharing log definitions

Reference `Logsmith` in every project. The runtime package includes the source generator as a bundled analyzer. All projects share the same public types (`LogLevel`, `ILogSink`, `LogEntry`, etc.) and can define their own log method classes.

```
MyApp.sln
  MyApp.Core/          --> references Logsmith (defines RenderLog, AudioLog)
  MyApp.Networking/    --> references Logsmith (defines NetworkLog)
  MyApp.Host/          --> references Logsmith (initializes LogManager, references Core + Networking)
```

The generator detects whether the `Logsmith` assembly is present in the compilation's references. If present, it emits only the partial method bodies and uses the public types from the assembly. If absent, it emits the full infrastructure as internal types.

---

## Compile-Time Diagnostics

The generator produces the following diagnostics:

| Code | Severity | Description |
|---|---|---|
| LSMITH001 | Error | Placeholder `{name}` in message template has no matching parameter. |
| LSMITH002 | Warning | Parameter is not referenced in the message template and is not a special type (Exception, caller info, ILogSink). |
| LSMITH003 | Error | Log method must be `static partial` in a `partial` class. |
| LSMITH004 | Error | Parameter type does not implement `IUtf8SpanFormattable`, `ISpanFormattable`, `IFormattable`, or `ToString()`. |
| LSMITH005 | Warning | Parameter has a `[Caller*]` attribute and also appears in the message template. Caller attribute takes priority. |
| LSMITH006 | Warning | `:json` format specifier on primitive type is unnecessary — prefer default formatting. |

---

## Extending Logsmith

### Custom sinks

Implement `ILogSink` for text output or `IStructuredLogSink` for structured property access:

```csharp
public sealed class CustomSink : ILogSink
{
    public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // Write the formatted message to your target
    }

    public void Dispose() { }
}
```

Register at initialization:

```csharp
Log.Initialize(config =>
{
    config.AddSink(new CustomSink());
});
```

### Sink base classes

`TextLogSink` and `BufferedLogSink` provide common patterns. `BufferedLogSink` uses a `Channel<T>`-based async queue so the calling thread never blocks on I/O:

```csharp
public sealed class MyNetworkSink : BufferedLogSink
{
    protected override void Flush(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // Called on background thread, safe to do I/O
    }
}
```

---

## Testing

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

### Testing with explicit sink parameter

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

---

## Configuration Reference

### LogManager initialization

```csharp
Log.Initialize(config =>
{
    // Global minimum level (default: Information)
    config.MinimumLevel = LogLevel.Debug;

    // Per-category minimum level override
    config.SetMinimumLevel("Renderer", LogLevel.Trace);
    config.SetMinimumLevel("Network", LogLevel.Warning);

    // Internal error handler for sink exceptions
    config.InternalErrorHandler = ex => Console.Error.WriteLine($"Logging error: {ex}");

    // Add sinks (all accept optional ILogFormatter parameter)
    config.AddConsoleSink();
    config.AddConsoleSink(colored: false, formatter: NullLogFormatter.Instance);
    config.AddFileSink("logs/app.log");
    config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
    config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Hourly,
                       maxFileSizeBytes: 50_000_000);
    config.AddFileSink("logs/app.log", shared: true);  // multi-process safe
    config.AddStreamSink(networkStream, leaveOpen: true);
    config.AddDebugSink();
    config.AddSink(new CustomSink());
});
```

### Runtime reconfiguration

```csharp
LogManager.Reconfigure(config =>
{
    config.ClearSinks();
    config.MinimumLevel = LogLevel.Warning;
    config.AddConsoleSink();
});
```

The configuration object is immutable. Reconfiguration builds a new config and swaps it atomically via a volatile write. The hot path reads the config through a single volatile read with no locking.

### MSBuild properties

```xml
<PropertyGroup>
    <!-- Conditional compilation threshold (default: Debug) -->
    <LogsmithConditionalLevel>Debug</LogsmithConditionalLevel>
</PropertyGroup>
```

---

## Architecture

### Package structure

The `Logsmith` NuGet package contains the runtime library in `lib/net10.0/` and the source generator in `analyzers/dotnet/cs/`. Referencing `Logsmith` provides both.

The `Logsmith.Generator` NuGet package contains only the source generator in `analyzers/dotnet/cs/`. It embeds the Logsmith runtime source files as resources. When the generator detects that the `Logsmith` assembly is not referenced, it emits these embedded sources as internal types. This ensures the standalone internal types are always identical to the public types in the runtime library.

### Generated code

For each `[LogMessage]`-decorated partial method, the generator emits:

- A level-guard early return (`if (!LogManager.IsEnabled(level)) return`).
- Stack-allocated UTF8 buffer and `Utf8LogWriter` construction.
- Alternating literal UTF8 writes and typed `WriteFormatted` calls for each template segment.
- A `LogEntry` construction with compile-time constants for category, event ID, and source location.
- Dispatch to `LogManager.Dispatch` with both the text span and a static property-writing delegate for structured sinks.
- `[Conditional("DEBUG")]` when the method's level falls at or below the configured threshold.

### Parameter classification

The generator classifies each method parameter by inspecting its type and attributes:

| Classification | Detection | Handling |
|---|---|---|
| Sink | Type is `ILogSink` | Used as dispatch target instead of LogManager |
| Exception | Type is or derives from `Exception` | Attached to `LogEntry.Exception`, excluded from message |
| CallerFile | Has `[CallerFilePath]` | Attached to `LogEntry.CallerFile` |
| CallerLine | Has `[CallerLineNumber]` | Attached to `LogEntry.CallerLine` |
| CallerMember | Has `[CallerMemberName]` | Attached to `LogEntry.CallerMember` |
| Message | Everything else | Matched to template placeholders, formatted to output |

Classification is by attribute and type, not by parameter position. Parameters of any classification can appear in any order.

### Performance characteristics

- **Hot path (logging enabled):** One volatile read (config), one enum comparison (level), stack-allocated buffer, direct UTF8 writes, no heap allocation for value-type parameters.
- **Hot path (logging disabled):** One volatile read, one enum comparison, return. No buffer allocation, no argument formatting.
- **Conditional-stripped methods:** Zero cost. Call site does not exist in the compiled IL. Arguments are not evaluated.
- **Config swap:** Single volatile write. No lock contention. Subsequent reads on any thread see the new config.

---

## License

This project is licensed under the [MIT License](LICENSE).
