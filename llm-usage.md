# Logsmith — LLM Quick Reference

Zero-allocation source-generated structured logging for .NET 10.

## Packages

| Package | Use | Output |
|---------|-----|--------|
| `Logsmith` | Multi-project; shared public types + bundled generator | Logsmith.dll in output |
| `Logsmith.Generator` | Single-project; embeds all types as `internal` | No runtime DLL |

Shared: `<PackageReference Include="Logsmith" Version="0.1.3" />`
Standalone: `<ProjectReference Include="Logsmith.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />`

## Declaring Log Methods

Methods must be `static partial` in a `partial class`.

```csharp
[LogCategory("MyApp")] // optional; defaults to class name
public static partial class Log
{
    [LogMessage(LogLevel.Information, "User {userId} logged in")]
    public static partial void UserLoggedIn(int userId);

    [LogMessage(LogLevel.Debug)] // auto-message: "Processing itemId={itemId}"
    public static partial void Processing(string itemId);

    [LogMessage(LogLevel.Error, "Op failed for {op}")]
    public static partial void OpFailed(string op, Exception ex);
}
```

### `[LogMessage]` Parameters

| Param | Type | Default | Notes |
|-------|------|---------|-------|
| `level` | `LogLevel` | required | Trace/Debug/Information/Warning/Error/Critical |
| `message` | `string` | `""` | Template with `{param}` placeholders; empty = auto-generated |
| `EventId` | `int` | `0` | 0 = auto-hashed from method name |
| `AlwaysEmit` | `bool` | `false` | Exempt from conditional compilation stripping |

### Special Method Parameters

- **`Exception`** — attached to `LogEntry.Exception`, excluded from text output
- **`[CallerFilePath]`/`[CallerLineNumber]`/`[CallerMemberName]`** — compiler-filled caller info
- **`ILogSink`** (first param) — routes directly to that sink, bypasses LogManager
- **`in T`** — pass large structs by ref to avoid copies

### Message Templates

- `{param}` placeholders match parameters case-insensitively
- Format specifiers: `{price:F2}`, `{date:yyyy-MM-dd}`
- JSON serialization: `{obj:json}` (allocates; warns LSMITH006 on primitives)
- Omit message string → auto-generates `"MethodName param1={param1} param2={param2}"`

## Initialization

```csharp
LogManager.Initialize(c =>
{
    c.MinimumLevel = LogLevel.Debug;
    c.AddConsoleSink(colored: true);
    c.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
    c.SetMinimumLevel("NoisyCategory", LogLevel.Warning); // by string
    c.SetMinimumLevel<Log>(LogLevel.Warning); // by type (uses generated CategoryName constant)
    c.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
});
```

`LogManager.Reconfigure(...)` — swap config at runtime (same API).
`LogManager.IsEnabled(LogLevel)` — check before expensive computations.
`LogManager.IsEnabled(LogLevel, string category)` — check with per-category override.

Each generated log class emits `public const string CategoryName` with the resolved category (from `[LogCategory]` or class name).

## LogLevel Enum

`Trace(0) < Debug(1) < Information(2) < Warning(3) < Error(4) < Critical(5) < None(6)`

## Built-In Sinks

```
AddConsoleSink(bool colored = true, ILogFormatter? formatter = null)
AddFileSink(string path, ILogFormatter? formatter = null, bool shared = false,
            RollingInterval rollingInterval = None, long maxFileSizeBytes = 10MB)
AddDebugSink(ILogFormatter? formatter = null)
AddStreamSink(Stream stream, bool leaveOpen = false, ILogFormatter? formatter = null)
AddSink(ILogSink sink)        // any custom sink
ClearSinks()
```

**FileSink features:** async-buffered, size+time rolling, `shared: true` for multi-process.
**RollingInterval:** `None | Hourly | Daily | Weekly | Monthly`

### RecordingSink (Testing)

```csharp
var sink = new RecordingSink();
LogManager.Initialize(c => c.AddSink(sink));
Log.SomeMethod();
Assert.Equal(1, sink.Entries.Count);
sink.Clear();
```

## Custom Sinks

Implement `ILogSink`: `bool IsEnabled(LogLevel)`, `void Write(in LogEntry, ReadOnlySpan<byte> utf8Message)`.
Extend `TextLogSink` (sync text) or `BufferedLogSink` (async/channel-based).
For JSON: implement `IStructuredLogSink.WriteStructured<TState>(...)`.

## Custom Type Formatting

- **Text:** implement `IUtf8SpanFormattable` on your type (zero-alloc UTF8 write)
- **Structured/JSON:** implement `ILogStructurable.WriteStructured(Utf8JsonWriter)`

## Compile-Time Level Stripping

```xml
<LogsmithConditionalLevel>Debug</LogsmithConditionalLevel>
```

Methods at/below threshold get `[Conditional("DEBUG")]` — removed from Release builds.
Values: `Trace | Debug (default) | Information | None`. Override per-method with `AlwaysEmit = true`.

## Diagnostics

| Code | Sev | Meaning |
|------|-----|---------|
| LSMITH001 | Error | Template placeholder has no matching parameter |
| LSMITH002 | Warn | Parameter unused in template (not Exception/caller/sink) |
| LSMITH003 | Error | Method must be `static partial` in `partial class` |
| LSMITH004 | Error | Parameter type lacks formatting support |
| LSMITH005 | Warn | Caller attribute + template placeholder conflict |
| LSMITH006 | Warn | `:json` format on primitive type (unnecessary allocation) |

## Formatting

Default output: `[12:34:56.789 INF Category] Message`
File output adds date: `[2026-02-18 12:34:56.789 INF Category] Message`
Custom: implement `ILogFormatter` with `FormatPrefix`/`FormatSuffix` methods writing to `IBufferWriter<byte>`.
`NullLogFormatter.Instance` — raw message only.

## LogEntry Fields

`Level`, `EventId`, `TimestampTicks` (UTC), `Category`, `Exception?`, `CallerFile?`, `CallerLine`, `CallerMember?`, `ThreadId`, `ThreadName?`
