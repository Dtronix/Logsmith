# <img src="./docs/images/logo-small.png" height="48"> Logsmith [![CI](https://github.com/Dtronix/Logsmith/actions/workflows/ci.yml/badge.svg)](https://github.com/Dtronix/Logsmith/actions/workflows/ci.yml)

Zero-allocation, source-generated structured logging for .NET 10.

Logsmith is a logging framework where the source generator *is* the framework. Every log method is analyzed at compile time, and the generator emits fully specialized, zero-allocation UTF8 code tailored to your exact parameters. No reflection. No boxing. No runtime parsing of message templates.

---

## Packages

| Name | NuGet | Description |
|------|-------|-------------|
| [`Logsmith`](https://www.nuget.org/packages/Logsmith) | [![Logsmith](https://img.shields.io/nuget/v/Logsmith.svg?maxAge=60)](https://www.nuget.org/packages/Logsmith) | Runtime library + bundled source generator. Default mode: **Shared**. |
| [`Logsmith.Generator`](https://www.nuget.org/packages/Logsmith.Generator) | [![Logsmith.Generator](https://img.shields.io/nuget/v/Logsmith.Generator.svg?maxAge=60)](https://www.nuget.org/packages/Logsmith.Generator) | Thin meta-package. Depends on `Logsmith` for the generator and build assets. Default mode: **Standalone** (zero runtime dependency). |
| [`Logsmith.Extensions.Logging`](https://www.nuget.org/packages/Logsmith.Extensions.Logging) | [![Logsmith.Extensions.Logging](https://img.shields.io/nuget/v/Logsmith.Extensions.Logging.svg?maxAge=60)](https://www.nuget.org/packages/Logsmith.Extensions.Logging) | Microsoft.Extensions.Logging bridge. Routes `ILogger` calls through Logsmith sinks. |

---

## Operating Modes

Logsmith supports three modes, controlled by the `<LogsmithMode>` MSBuild property:

| Mode | Default for | Runtime DLL | Generated types | Use case |
|------|------------|-------------|-----------------|----------|
| **Shared** | `Logsmith` package | Yes, flows transitively | Method bodies only | Applications and multi-project solutions |
| **Standalone** | `Logsmith.Generator` package | No (`PrivateAssets="all"`) | All types as `internal` | Libraries that want zero transitive dependencies |
| **Abstraction** | Explicit opt-in | No (`PrivateAssets="all"`) | Public interfaces + internal infrastructure | Libraries that expose logging contracts to consumers |

### Mode precedence

When both packages are referenced transitively, `Shared` wins (NuGet evaluates `Logsmith.props` before `Logsmith.Generator.props`). Explicit `<LogsmithMode>` in your `.csproj` always overrides.

### PrivateAssets requirement

In Standalone or Abstraction mode, the Logsmith runtime DLL must not leak to consumers. Add `PrivateAssets="all"` to the package reference. The build emits **LSMITH010** if this is missing.

```xml
<PackageReference Include="Logsmith" Version="0.5.0" PrivateAssets="all" />
```

---

## Quick Start

### 1. Install

```xml
<PackageReference Include="Logsmith" Version="0.5.0" />
```

### 2. Initialize

```csharp
LogManager.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Debug;
    config.AddConsoleSink();
    config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
});
```

### 3. Declare log methods

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

### 4. Call them

```csharp
RenderLog.DrawCallCompleted(42, 1.5);
```

No logger injection. No service locator. The generated code dispatches through the static `LogManager`.

---

## Declaring Log Methods

Methods must be `static partial` in a `partial` class returning `void`.

```csharp
[LogMessage(LogLevel.Information, "Connection to {endpoint} in {latencyMs}ms")]
public static partial void Connected(string endpoint, double latencyMs);
```

### Template-free mode

Omit the message string. The generator constructs it from the method name and parameters:

```csharp
[LogMessage(LogLevel.Debug)]
public static partial void FrameRendered(int frameId, long triangleCount);
// Generated: "FrameRendered frameId={frameId} triangleCount={triangleCount}"
```

### Categories

`[LogCategory("Name")]` sets the category on all log entries from that class. Defaults to the class name.

### EventId

Stable hash of the qualified method name by default. Override with `EventId = 5001`.

---

## Log Levels and Conditional Compilation

```
Trace < Debug < Information < Warning < Error < Critical < None
```

### Runtime filtering

```csharp
config.MinimumLevel = LogLevel.Information;
config.SetMinimumLevel<RenderLog>(LogLevel.Debug); // per-category override
```

### Compile-time stripping

Methods at or below the threshold receive `[Conditional("DEBUG")]`, erasing call sites from release builds:

```xml
<LogsmithConditionalLevel>Debug</LogsmithConditionalLevel> <!-- default -->
```

Exempt specific methods with `AlwaysEmit = true`.

---

## Sinks

### Built-in sinks

| Sink | Description |
|------|-------------|
| `ConsoleSink` | ANSI-colored UTF8 output via `Console.OpenStandardOutput()` |
| `FileSink` | Async-buffered, supports rolling (time/size), shared multi-process mode |
| `StreamSink` | Async-buffered output to any `Stream` |
| `DebugSink` | `System.Diagnostics.Debug` output |
| `RecordingSink` | In-memory capture for test assertions |
| `NullSink` | No-op for benchmarking |

### Structured sinks

Implement `IStructuredLogSink` to receive typed properties via `Utf8JsonWriter`:

```csharp
public sealed class JsonSink : IStructuredLogSink
{
    public bool IsEnabled(LogLevel level) => true;
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message) { }
    public void WriteStructured<TState>(in LogEntry entry, TState state,
        WriteProperties<TState> propertyWriter) where TState : allows ref struct
    {
        using var writer = new Utf8JsonWriter(stream);
        writer.WriteStartObject();
        propertyWriter(writer, state); // writes each param as a named JSON property
        writer.WriteEndObject();
    }
    public void Dispose() { }
}

config.AddSink(new JsonSink());
```

### Sink filtering by category

```csharp
config.AddFileSink("logs/render.log", category: "Renderer");
```

### Flushing and shutdown

```csharp
await LogManager.FlushAsync();                    // flush all IFlushableLogSink instances
await LogManager.ShutdownAsync(TimeSpan.FromSeconds(5)); // drain + dispose all sinks
```

---

## Format Specifiers

Standard .NET format strings and `:json` serialization in message templates:

```csharp
[LogMessage(LogLevel.Information, "Price={price:F2}")]
public static partial void LogPrice(decimal price);

[LogMessage(LogLevel.Debug, "Config={config:json}")]
public static partial void LogConfig(Dictionary<string, string> config);
```

`:json` uses `System.Text.Json.JsonSerializer` for both text and structured paths.

---

## Custom Type Serialization

### Text output (priority order)

1. `IUtf8SpanFormattable` — direct UTF8 write, zero allocation
2. `ISpanFormattable` — stack `Span<char>`, transcode to UTF8
3. `IFormattable` — `ToString(null, null)`
4. `ToString()` — last resort

### Structured output

Implement `ILogStructurable` for custom JSON representation:

```csharp
public class OrderInfo : ILogStructurable
{
    public string OrderId { get; init; }
    public void WriteStructured(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("orderId", OrderId);
        writer.WriteEndObject();
    }
}
```

---

## Scoped Context

`LogScope` provides `AsyncLocal`-based ambient properties on every log entry within a scope:

```csharp
using (LogScope.Push("RequestId", "req-abc-123"))
{
    Log.ProcessingOrder("ORD-001", "Alice");
    // Output includes [RequestId=req-abc-123]
}
```

Scopes nest, propagate through `async`/`await`, and are shared with the MEL bridge.

---

## Log Sampling and Rate Limiting

```csharp
[LogMessage(LogLevel.Debug, "Heartbeat", SampleRate = 10)]    // 1-in-10
public static partial void Heartbeat();

[LogMessage(LogLevel.Warning, "Throttled", MaxPerSecond = 100)] // cap at 100/sec
public static partial void Throttled();
```

Lock-free `Interlocked` operations. Zero cost when not configured.

---

## Dynamic Level Switching

```csharp
config.WatchEnvironmentVariable("MY_LOG_LEVEL");  // polls every 5s
config.WatchConfigFile("logsmith.json");           // FileSystemWatcher with debounce
```

Config file format:

```json
{
    "MinimumLevel": "Warning",
    "CategoryOverrides": { "Network": "Debug" }
}
```

---

## Abstraction Mode (Library Authors)

Abstraction mode lets library authors expose a logging interface without imposing `Logsmith.dll` on consumers.

### Setup

```xml
<PropertyGroup>
    <LogsmithMode>Abstraction</LogsmithMode>
    <!-- Optional: custom namespace (default: {RootNamespace}.Logging) -->
    <LogsmithNamespace>MyLib.Logging</LogsmithNamespace>
</PropertyGroup>

<PackageReference Include="Logsmith" Version="0.5.0" PrivateAssets="all" />
```

### Generated public types

The generator emits these as **public** types in the configured namespace:

- `ILogsmithLogger` — text-only logging interface
- `IStructuredLogsmithLogger` — extends `ILogsmithLogger` with typed property access
- `LogsmithOutput` — static `Logger` property for wiring at startup
- `LogLevel`, `LogEntry`, `LogScope`, `WriteProperties<TState>`

### Library declares log methods

```csharp
[LogCategory("MyLib")]
static partial class LibLog
{
    [LogMessage(LogLevel.Information, "Connected to {endpoint}")]
    public static partial void Connected(string endpoint);
}
```

### Consumer wires a logger

```csharp
using MyLib.Logging;

LogsmithOutput.Logger = new MyLogsmithLogger();
```

Text-only implementation:

```csharp
sealed class MyLogsmithLogger : ILogsmithLogger
{
    public bool IsEnabled(LogLevel level, string category) => true;
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        Console.WriteLine(Encoding.UTF8.GetString(utf8Message));
    }
}
```

Structured implementation (optional):

```csharp
sealed class MyStructuredLogger : IStructuredLogsmithLogger
{
    public bool IsEnabled(LogLevel level, string category) => true;
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message) { /* ... */ }
    public void WriteStructured<TState>(in LogEntry entry, ReadOnlySpan<byte> utf8Message,
        TState state, WriteProperties<TState> propertyWriter) where TState : allows ref struct
    {
        // Receives typed properties via propertyWriter
    }
}
```

Generated methods automatically detect `IStructuredLogsmithLogger` at runtime and dispatch to `WriteStructured` when available.

---

## Microsoft.Extensions.Logging Bridge

```xml
<PackageReference Include="Logsmith.Extensions.Logging" Version="0.5.0" />
```

```csharp
services.AddLogging(builder => builder.AddLogsmith());
```

MEL `BeginScope` delegates to `LogScope.Push`, so scopes are shared between MEL and direct Logsmith calls.

---

## Performance: `in` Parameters

Pass large structs by reference to avoid copying:

```csharp
[LogMessage(LogLevel.Information, "Sensor reported {reading}")]
public static partial void SensorData(in SensorReading reading);
```

---

## Explicit Sink Parameter

Route to a specific sink instead of `LogManager`:

```csharp
[LogMessage(LogLevel.Debug, "Test event: {value}")]
public static partial void TestEvent(ILogSink sink, int value);
```

---

## Global Exception Handler

```csharp
config.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
config.CaptureUnhandledExceptions(observeTaskExceptions: true);
```

Wires `AppDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`. Lifecycle tied to `Reconfigure()` and `Shutdown()`.

---

## Compile-Time Diagnostics

| Code | Severity | Description |
|---|---|---|
| LSMITH001 | Error | Placeholder `{name}` has no matching parameter |
| LSMITH002 | Warning | Parameter not referenced in template |
| LSMITH003 | Error | Method must be `static partial` in a `partial` class |
| LSMITH004 | Error | Parameter type has no supported formatting path |
| LSMITH005 | Warning | Caller info parameter also in template (caller attribute wins) |
| LSMITH006 | Warning | `:json` on primitive type is unnecessary |
| LSMITH007 | Warning | Both `SampleRate` and `MaxPerSecond` set |
| LSMITH008 | Warning | `ILogSink` parameter in abstraction mode (use `ILogsmithLogger`) |
| LSMITH010 | Warning | Standalone/Abstraction mode without `PrivateAssets="all"` |

---

## MSBuild Properties

| Property | Values | Default |
|----------|--------|---------|
| `LogsmithMode` | `Shared`, `Standalone`, `Abstraction` | `Shared` (Logsmith pkg) / `Standalone` (Logsmith.Generator pkg) |
| `LogsmithConditionalLevel` | `Trace`, `Debug`, `Information`, `None` | `Debug` |
| `LogsmithNamespace` | Any namespace | `{RootNamespace}.Logging` (Abstraction mode only) |

---

## Comparison with Other Frameworks

| Capability | Logsmith | MEL + LoggerMessage | ZLogger | Serilog | NLog |
|---|---|---|---|---|---|
| Source-generated method bodies | Yes | Yes | Yes | No | No |
| Zero runtime dependency mode | Yes | No | No | No | No |
| Abstraction mode for libraries | Yes | No | No | No | No |
| Zero allocation hot path | Yes | Partial | Yes | No | No |
| UTF8 end-to-end | Yes | No | Yes | No | No |
| Structured logging | Yes (Utf8JsonWriter) | Yes | Yes | Yes | Yes |
| Compile-time level stripping | Yes | No | No | No | No |
| No boxing of value types | Yes | Yes | Yes | No | No |
| NativeAOT compatible | Yes | Yes | Yes | Partial | Partial |
| Log sampling / rate limiting | Yes | No | No | No | No |
| MEL bridge | Yes | Native | Native | Yes | Yes |

---

## Benchmarks

Full results comparing Logsmith against MEL, Serilog, NLog, and ZLogger: [docs/benchmarks.md](docs/benchmarks.md).

---

## License

[MIT License](LICENSE)
