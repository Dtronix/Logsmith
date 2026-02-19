# Logsmith — LLM Reference

Zero-allocation, source-generator-driven C# logging library. .NET 10+.

## Architecture

Two NuGet packages, two modes:
- **Shared mode**: Reference `Logsmith` (runtime + generator). Generator emits only method bodies.
- **Standalone mode**: Reference only `Logsmith.Generator` as analyzer. Generator embeds all runtime types as `internal`.

Pipeline: `[LogMessage]` partial methods → Roslyn IncrementalGenerator → UTF-8 text path + JSON structured path.
Generator emits `public const string CategoryName` on each log class for type-safe per-category configuration.

## Project Layout

```
src/Logsmith/                    Runtime library (net10.0)
  LogLevel.cs                    enum byte: Trace=0..Critical=5, None=6
  LogEntry.cs                    readonly struct: Level,EventId,TimestampTicks,Category,Exception?,CallerFile?,CallerLine,CallerMember?,ThreadId,ThreadName?
  LogManager.cs                  static: Initialize(Action<LogConfigBuilder>), Reconfigure(...), IsEnabled(LogLevel), IsEnabled(LogLevel, string category), Dispatch<TState>(...) with try/catch error handler
  LogConfigBuilder.cs            Fluent builder: MinimumLevel, InternalErrorHandler, AddSink(), AddConsoleSink(), AddFileSink(), AddDebugSink(), AddStreamSink(), SetMinimumLevel(category,level), SetMinimumLevel<T>(level), ClearSinks()
  Utf8LogWriter.cs               ref struct: Write(ROSpan<byte>), WriteFormatted<T>(in T), WriteFormatted<T>(in T, ROSpan<char> format), WriteString(string?), GetWritten()
  Attributes/
    LogMessageAttribute.cs       [LogMessage(LogLevel, message?, EventId=, AlwaysEmit=)] on methods
    LogCategoryAttribute.cs      [LogCategory("name")] on classes
  Sinks/
    ILogSink.cs                  IsEnabled(LogLevel), Write(in LogEntry, ROSpan<byte>), IDisposable
    IStructuredLogSink.cs        extends ILogSink: WriteStructured<TState>(in LogEntry, TState, WriteProperties<TState>)
    ILogStructurable.cs          WriteStructured(Utf8JsonWriter)
    WriteProperties.cs           delegate void WriteProperties<TState>(Utf8JsonWriter, TState) where TState: allows ref struct
    TextLogSink.cs               abstract base: MinimumLevel, abstract WriteMessage(...)
    BufferedLogSink.cs           abstract async base: Channel<BufferedEntry>, abstract WriteBufferedAsync(...)
    NullSink.cs                  IsEnabled→false, no-op
    DebugSink.cs                 Debugger.IsAttached guard → Debug.WriteLine, ILogFormatter
    ConsoleSink.cs               extends TextLogSink: ANSI colored, stdout stream, ILogFormatter
    FileSink.cs                  extends BufferedLogSink: async file I/O, size+time rolling, shared mode, ILogFormatter
    StreamSink.cs                extends BufferedLogSink: writes to any Stream, leaveOpen, ILogFormatter
    RecordingSink.cs             List<CapturedEntry> for testing, record CapturedEntry(...) with ThreadId/ThreadName
    RollingInterval.cs           enum: None, Hourly, Daily, Weekly, Monthly
  Formatting/
    ILogFormatter.cs             FormatPrefix(in LogEntry, IBufferWriter<byte>), FormatSuffix(...)
    DefaultLogFormatter.cs       [HH:mm:ss.fff LVL Category] prefix, newline+exception suffix, includeDate option
    NullLogFormatter.cs          No-op formatter (raw message only)
  Internal/
    SinkSet.cs                   Classify(List<ILogSink>)→TextSinks[]+StructuredSinks[]
    LogConfig.cs                 MinimumLevel, CategoryOverrides, SinkSet, ErrorHandler

src/Logsmith.Generator/          Source generator (netstandard2.0, Roslyn 4.3)
  LogsmithGenerator.cs           IIncrementalGenerator: syntax predicate→transform→per-class emission
  ModeDetector.cs                IsSharedMode(): checks for "Logsmith" assembly reference
  EventIdGenerator.cs            FNV-1a hash of "Class.Method" or user-specified
  ConditionalCompilation.cs      [Conditional("DEBUG")] for methods ≤ threshold level
  Models/
    LogMethodInfo.cs             All method metadata: namespace, class, params, template, mode, level, eventId
    ParameterInfo.cs             Name, TypeFullName, Kind, IsNullableValueType, IsNullableReferenceType, defaults, RefKind
    ParameterKind.cs             enum: MessageParam, Sink, Exception, CallerFile, CallerLine, CallerMember
    TemplatePart.cs              IsPlaceholder, Text, FormatSpecifier?, BoundParameter?
  Parsing/
    TemplateParser.cs            Parse(template)→parts (with format specifiers), Bind(parts,params)→diagnostics, GenerateTemplateFree(...)
    ParameterClassifier.cs       Classify(IMethodSymbol): Sink(idx0)→Exception→CallerInfo→MessageParam
  Emission/
    MethodEmitter.cs             EmitClassFile(), EmitMethodBody(): early-exit guard→LogEntry→stackalloc→Utf8LogWriter→Dispatch
    TextPathEmitter.cs           writer.Write("..."u8) for literals, WriteFormatted/WriteString for params, :json→SerializeToUtf8Bytes
    StructuredPathEmitter.cs     WriteProperties_<Method>(Utf8JsonWriter, State): JSON property writes, format-aware, :json→Serialize
    NullableEmitter.cs           EmitNullGuard(): HasValue/is not null/direct
    EmbeddedSourceEmitter.cs     Standalone: embeds all Logsmith/*.cs, replaces public→internal
    SerializationKind.cs         enum: Utf8SpanFormattable, SpanFormattable, Formattable, String, ToString
  Diagnostics/
    DiagnosticDescriptors.cs     LSMITH001-006: unmatched placeholder, unreferenced param, not static partial, no format path, caller info in template, :json on primitive
```

## Usage

### Define log methods

```csharp
[LogCategory("MyApp")]
public static partial class Log
{
    // Explicit template
    [LogMessage(LogLevel.Information, "Started with {argCount} arg(s)")]
    public static partial void AppStarted(int argCount);

    // Template-free (auto-generates "MethodName param={param}")
    [LogMessage(LogLevel.Debug)]
    public static partial void ProcessingItem(string itemId, int count);

    // With exception
    [LogMessage(LogLevel.Error, "Operation {name} failed")]
    public static partial void OpFailed(string name, Exception ex);

    // Caller info
    [LogMessage(LogLevel.Trace, "Checkpoint")]
    public static partial void Checkpoint(
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0,
        [CallerMemberName] string? member = null);

    // Nullable params
    [LogMessage(LogLevel.Warning, "Missing value: {key}={value}")]
    public static partial void MissingValue(string key, int? value);

    // 'in' parameter — passes large struct by reference, avoids copying
    [LogMessage(LogLevel.Information, "Sensor reported {reading}")]
    public static partial void SensorData(in SensorReading reading);

    // Format specifiers
    [LogMessage(LogLevel.Information, "Price={price:F2}")]
    public static partial void LogPrice(decimal price);

    // JSON serialization via :json
    [LogMessage(LogLevel.Debug, "Config={config:json}")]
    public static partial void LogConfig(object config);

    // Explicit sink (bypasses LogManager, sink must be first param)
    [LogMessage(LogLevel.Information, "Direct: {msg}")]
    public static partial void DirectLog(ILogSink sink, string msg);
}
```

### Configure and use

```csharp
LogManager.Initialize(cfg =>
{
    cfg.MinimumLevel = LogLevel.Debug;
    cfg.AddConsoleSink(colored: true);
    cfg.AddFileSink("app.log", shared: true,
        rollingInterval: RollingInterval.Daily,
        maxFileSizeBytes: 50 * 1024 * 1024);
    cfg.AddDebugSink();
    cfg.AddStreamSink(networkStream, leaveOpen: true);
    cfg.SetMinimumLevel("Noisy", LogLevel.Warning); // per-category by string
    cfg.SetMinimumLevel<Log>(LogLevel.Warning); // per-category by type (uses generated CategoryName constant)
    cfg.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
});

Log.AppStarted(args.Length);
Log.OpFailed("save", ex);

// Runtime reconfiguration
LogManager.Reconfigure(cfg =>
{
    cfg.MinimumLevel = LogLevel.Warning;
    cfg.AddConsoleSink();
});
```

### Standalone mode (no Logsmith runtime reference)

```xml
<ItemGroup>
  <ProjectReference Include="Logsmith.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

All runtime types are embedded as `internal` automatically.

## Generator Internals

### Generated code structure (per method)

1. `[Conditional("DEBUG")]` if level ≤ threshold (MSBuild `<LogsmithConditionalLevel>`, default: Debug)
2. Early-exit: `if (!LogManager.IsEnabled(level)) return;` (or `!sink.IsEnabled()` for explicit sink)
3. Construct `LogEntry` with UTC ticks, event ID, category, exception, caller info, threadId, threadName
4. `stackalloc byte[128..4096]` → `Utf8LogWriter` → write template parts as UTF-8
5. Dispatch: `LogManager.Dispatch(in entry, utf8Message, state, WriteProperties_Method)` or `sink.Write(in entry, utf8Message)`
6. State ref struct holds message params for structured sink path

### Buffer sizing

Literals: byte count. String params: +128. Other params: +32. `:json` params: +256. Clamped [128, 4096].

### Diagnostics

| Code | Severity | Description |
|------|----------|-------------|
| LSMITH001 | Error | Placeholder `{x}` has no matching parameter |
| LSMITH002 | Warning | Parameter not referenced in template |
| LSMITH003 | Error | Method must be `static partial` in `partial class` |
| LSMITH004 | Error | Parameter type has no supported formatting path |
| LSMITH005 | Warning | Caller info param name used as template placeholder |
| LSMITH006 | Warning | `:json` format specifier on primitive type is unnecessary |

## Key Design Decisions

- UTF-8 first: `Utf8LogWriter` ref struct writes directly to stack buffer, no string intermediary
- Dual dispatch: `SinkSet` classifies sinks once at config time into text[] and structured[] arrays
- Thread safety: `LogManager` uses `volatile` config + `Interlocked.CompareExchange` for init guard
- Sink first-param convention: explicit `ILogSink` parameter must be at index 0, bypasses `LogManager`
- `in` parameter support: `ParameterInfo.RefKind` ("in " or ""), preserved on method signature, state struct ctor, and state construction call
- CategoryName constant: generator emits `public const string CategoryName` per class, used by `SetMinimumLevel<T>()` to resolve category without magic strings
- Per-category filtering: `IsEnabled(LogLevel, string category)` checks `CategoryOverrides` before global minimum; generated code passes category to this overload
- EventId: user-specified if nonzero, else stable FNV-1a hash of `"ClassName.MethodName"`
- Visibility transform: standalone mode replaces `public` → `internal`, `protected` → `private protected`
- Thread info: `LogEntry.ThreadId`/`ThreadName` captured at call site. Not rendered in text output by default — available for structured sinks and explicit `{threadId}`/`{threadName}` template placeholders
- Error handler: `LogConfigBuilder.InternalErrorHandler` — sink exceptions caught in Dispatch, routed to handler or silently swallowed
- Format specifiers: `{value:F2}` for standard .NET format strings, `{obj:json}` for JSON serialization — parsed at compile-time, emitted as static code
- ILogFormatter: pluggable prefix/suffix formatting via `IBufferWriter<byte>`. DefaultLogFormatter for timestamps/level/category, NullLogFormatter for raw output
- FileSink shared mode: `FileShare.ReadWrite` for multi-process append, seek-to-end before write, safe concurrent rolling
- Time-interval rolling: `RollingInterval` enum (None/Hourly/Daily/Weekly/Monthly), uses entry timestamp for testability

## Testing

- NUnit only (no FluentAssertions, no Moq)
- `tests/Logsmith.Tests/` — runtime sink behavior tests
- `tests/Logsmith.Generator.Tests/` — Roslyn compilation tests via `GeneratorTestHelper`
- `RecordingSink` captures entries in-memory for assertions
- `LogManager.Reset()` (internal) clears state between tests
