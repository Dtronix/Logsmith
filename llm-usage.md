# Logsmith Usage Reference

Zero-allocation, source-generator-driven C# logging. .NET 10+.

## Packages

| Package | Use |
|---------|-----|
| `Logsmith` | Shared mode: runtime + bundled generator |
| `Logsmith.Generator` | Standalone: all types embedded as `internal`, no runtime DLL |
| `Logsmith.Extensions.Logging` | MEL bridge: routes `ILogger` through Logsmith sinks |

## Log Methods

`static partial` methods with `[LogMessage]` in a `static partial class` with optional `[LogCategory]`:

```csharp
[LogCategory("MyApp")] // defaults to class name if omitted
public static partial class Log
{
    [LogMessage(LogLevel.Information, "User {userId} logged in")]
    public static partial void UserLoggedIn(int userId);

    [LogMessage(LogLevel.Debug)] // auto: "Processing itemId={itemId}"
    public static partial void Processing(string itemId);

    [LogMessage(LogLevel.Error, "Op failed for {op}")]
    public static partial void OpFailed(string op, Exception ex);

    // Sampling (1-in-100) and rate-limiting (max 10/sec)
    [LogMessage(LogLevel.Debug, "Tick={tick}", SampleRate = 100)]
    public static partial void SampledTick(long tick);
    [LogMessage(LogLevel.Warning, "Throttled={id}", MaxPerSecond = 10)]
    public static partial void RateLimited(string id);
}
```

### `[LogMessage]` Properties

| Property | Type | Default | Notes |
|----------|------|---------|-------|
| `level` | `LogLevel` | required | Trace/Debug/Information/Warning/Error/Critical |
| `message` | `string` | `""` | `{param}` placeholders; empty = auto-generated |
| `EventId` | `int` | `0` | 0 = FNV-1a hash of `Class.Method` |
| `AlwaysEmit` | `bool` | `false` | Exempt from conditional compilation stripping |
| `SampleRate` | `int` | `0` | Emit 1-in-N calls (atomic counter). Cannot combine with MaxPerSecond |
| `MaxPerSecond` | `int` | `0` | Sliding-second rate limit. Cannot combine with SampleRate (LSMITH007) |

### Special Parameters

- **`Exception`** — attached to `LogEntry.Exception`, excluded from message text
- **`[CallerFilePath]`/`[CallerLineNumber]`/`[CallerMemberName]`** — compiler-filled caller info
- **`ILogSink`** (first param) — routes directly to that sink, bypasses LogManager
- **`in T`** — pass large structs by ref to avoid copies

### Message Templates

- `{param}` binds by name (case-insensitive)
- Format specifiers: `{price:F2}`, `{date:yyyy-MM-dd}`
- JSON: `{obj:json}` (allocates; LSMITH006 warns on primitives)
- Omit message → auto-generates `"MethodName param1={param1} param2={param2}"`

## Configuration

```csharp
LogManager.Initialize(c =>
{
    c.MinimumLevel = LogLevel.Debug;
    c.AddConsoleSink(colored: true);
    c.AddFileSink("app.log", shared: true, rollingInterval: RollingInterval.Daily, maxFileSizeBytes: 50_000_000);
    c.AddDebugSink();
    c.AddStreamSink(stream, leaveOpen: true);
    c.SetMinimumLevel("NoisyCategory", LogLevel.Warning);
    c.SetMinimumLevel<Log>(LogLevel.Warning); // type-safe via generated CategoryName
    c.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
    c.WatchEnvironmentVariable("LOGSMITH_LEVEL"); // dynamic level via env var polling
    c.WatchConfigFile("logsmith.json"); // dynamic level via JSON: {MinimumLevel, CategoryOverrides}
});
```

`LogManager.Reconfigure(...)` — swap config at runtime (same builder API).

### LogConfigBuilder API

| Method | Notes |
|--------|-------|
| `MinimumLevel` | Global minimum LogLevel property |
| `AddConsoleSink(colored?, minimumLevel?, formatter?)` | ANSI-colored stdout |
| `AddFileSink(path, minimumLevel?, formatter?, shared?, rollingInterval?, maxFileSizeBytes?)` | Async buffered, size+time rolling, `shared:true` for multi-process |
| `AddDebugSink(minimumLevel?, formatter?)` | `Debug.WriteLine`, debugger-only |
| `AddStreamSink(stream, minimumLevel?, formatter?, leaveOpen?, capacity?)` | Any `Stream`, async buffered |
| `AddSink(ILogSink)` | Custom sink |
| `ClearSinks()` | Remove all |
| `SetMinimumLevel(category, level)` | Per-category by string |
| `SetMinimumLevel<T>(level)` | Per-category by type (`T.CategoryName`) |
| `InternalErrorHandler` | `Action<Exception>` for sink errors |
| `WatchEnvironmentVariable(name?, pollInterval?)` | Polls env var, updates minimum level |
| `WatchConfigFile(path)` | Watches JSON file, updates levels + category overrides |

## Scoped Context

```csharp
using (LogScope.Push("RequestId", requestId))
{
    Log.Processing(itemId); // scope properties available to sinks
}
// Multi-property
using (LogScope.Push([new("UserId", uid), new("TenantId", tid)]))
{ ... }
```

Async-local linked-list stack. `EnumerateProperties()` returns `ScopeEnumerator` ref struct (zero-alloc).

## MEL Bridge

```csharp
builder.Logging.AddLogsmith(); // registers LogsmithLoggerProvider
```

## LogLevel

`Trace(0) < Debug(1) < Information(2) < Warning(3) < Error(4) < Critical(5) < None(6)`

## Built-In Sinks

**FileSink:** async-buffered, size+time rolling, `shared:true` for multi-process (named mutex).
**RollingInterval:** `None | Hourly | Daily | Weekly | Monthly`
**RecordingSink:** `List<CapturedEntry>` for testing — `sink.Entries`, `sink.Clear()`

## Custom Sinks

Implement `ILogSink`: `IsEnabled(LogLevel)`, `Write(in LogEntry, ROSpan<byte>)`, `IDisposable`.
Extend `TextLogSink` (sync) or `BufferedLogSink` (async channel-based).
For JSON: implement `IStructuredLogSink.WriteStructured<TState>(...)`.

## Formatting

Default: `[12:34:56.789 INF Category] Message`. File adds date prefix.
Custom: implement `ILogFormatter` (`FormatPrefix`/`FormatSuffix` to `IBufferWriter<byte>`).
`NullLogFormatter.Instance` — raw message only.

## Compile-Time Stripping

```xml
<LogsmithConditionalLevel>Debug</LogsmithConditionalLevel>
```

Methods at/below threshold get `[Conditional("DEBUG")]` — removed in Release. Override with `AlwaysEmit = true`.

## Diagnostics

| Code | Sev | Trigger |
|------|-----|---------|
| LSMITH001 | Error | Placeholder has no matching parameter |
| LSMITH002 | Warn | Parameter unused in template |
| LSMITH003 | Error | Method not `static partial` in `partial class` |
| LSMITH004 | Error | Parameter type lacks formatting support |
| LSMITH005 | Warn | Caller attribute + template placeholder conflict |
| LSMITH006 | Warn | `:json` on primitive type |
| LSMITH007 | Warn | Both SampleRate and MaxPerSecond set |

## LogEntry Fields

`Level`, `EventId`, `TimestampTicks` (UTC), `Category`, `Exception?`, `CallerFile?`, `CallerLine`, `CallerMember?`, `ThreadId`, `ThreadName?`
