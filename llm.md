# Logsmith — LLM Reference

Zero-allocation, source-generator-driven C# logging library. .NET 10+.

## Architecture

Three NuGet packages, two modes:
- **Shared mode**: Reference `Logsmith` (runtime + generator). Generator emits only method bodies.
- **Standalone mode**: Reference only `Logsmith.Generator` as analyzer. Generator embeds all runtime types as `internal`.
- **MEL bridge**: Reference `Logsmith.Extensions.Logging` to route `Microsoft.Extensions.Logging` through Logsmith sinks.

Pipeline: `[LogMessage]` partial methods → Roslyn IncrementalGenerator → UTF-8 text path + JSON structured path.
Generator emits `public const string CategoryName` on each log class for type-safe per-category configuration.

## Project Layout

```
src/Logsmith/                    Runtime library (net10.0)
  LogLevel.cs                    enum byte: Trace=0..Critical=5, None=6
  LogEntry.cs                    readonly struct: Level,EventId,TimestampTicks,Category,Exception?,CallerFile?,CallerLine,CallerMember?,ThreadId,ThreadName?
  LogManager.cs                  static: Initialize(), Reconfigure(), IsEnabled(), Dispatch<TState>(), SetMinimumLevel(), SetCategoryOverrides()
  LogConfigBuilder.cs            Fluent builder: MinimumLevel, InternalErrorHandler, AddSink(), AddConsoleSink(), AddFileSink(), AddDebugSink(), AddStreamSink(), SetMinimumLevel(), ClearSinks(), WatchEnvironmentVariable(), WatchConfigFile()
  LogScope.cs                    Ambient async-local scope: Push(key,value), Push(ROSpan<KVP>), EnumerateProperties()→ScopeEnumerator ref struct
  Utf8LogWriter.cs               ref struct: Write(ROSpan<byte>), WriteFormatted<T>(in T), WriteFormatted<T>(in T, ROSpan<char> format), WriteString(string?), GetWritten()
  Attributes/
    LogMessageAttribute.cs       [LogMessage(LogLevel, message?, EventId=, AlwaysEmit=, SampleRate=, MaxPerSecond=)]
    LogCategoryAttribute.cs      [LogCategory("name")] on classes
  Sinks/
    ILogSink.cs                  IsEnabled(LogLevel), Write(in LogEntry, ROSpan<byte>), IDisposable
    IStructuredLogSink.cs        extends ILogSink: WriteStructured<TState>(in LogEntry, TState, WriteProperties<TState>)
    ILogStructurable.cs          WriteStructured(Utf8JsonWriter)
    WriteProperties.cs           delegate void WriteProperties<TState>(Utf8JsonWriter, TState) where TState: allows ref struct
    TextLogSink.cs               abstract base: MinimumLevel, abstract WriteMessage(...)
    BufferedLogSink.cs           abstract async base: Channel<BufferedEntry>(capacity), abstract WriteBufferedAsync(...)
    NullSink.cs                  IsEnabled→false, no-op
    DebugSink.cs                 Debugger.IsAttached guard → Debug.WriteLine, ILogFormatter
    ConsoleSink.cs               extends TextLogSink: ANSI colored, stdout stream, ILogFormatter, ThreadBuffer pooling
    FileSink.cs                  extends BufferedLogSink: async file I/O, size+time rolling, shared mode (named mutex), ILogFormatter
    StreamSink.cs                extends BufferedLogSink: writes to any Stream, leaveOpen, ILogFormatter
    RecordingSink.cs             List<CapturedEntry> for testing, record CapturedEntry(...) with ThreadId/ThreadName
    RollingInterval.cs           enum: None, Hourly, Daily, Weekly, Monthly
  Formatting/
    ILogFormatter.cs             FormatPrefix(in LogEntry, IBufferWriter<byte>), FormatSuffix(...)
    DefaultLogFormatter.cs       [HH:mm:ss.fff LVL Category] prefix, newline+exception suffix, includeDate option, cached category UTF-8
    NullLogFormatter.cs          No-op formatter (raw message only)
  DynamicLevel/
    EnvironmentLevelMonitor.cs   internal: polls env var at interval, calls LogManager.SetMinimumLevel()
    FileLevelMonitor.cs          internal: watches JSON file {MinimumLevel, CategoryOverrides}, debounced 500ms
  Internal/
    SinkSet.cs                   Classify(List<ILogSink>)→TextSinks[]+StructuredSinks[]
    LogConfig.cs                 MinimumLevel, CategoryOverrides, SinkSet, ErrorHandler, Monitors[]
    ThreadBuffer.cs              internal static: thread-static ArrayBufferWriter<byte> pooling (512B default)

src/Logsmith.Generator/          Source generator (netstandard2.0, Roslyn 4.3)
  LogsmithGenerator.cs           IIncrementalGenerator: syntax predicate→transform→per-class emission
  ModeDetector.cs                IsSharedMode(): checks for "Logsmith" assembly reference
  EventIdGenerator.cs            FNV-1a hash of "Class.Method" or user-specified
  ConditionalCompilation.cs      [Conditional("DEBUG")] for methods ≤ threshold level
  Models/
    LogMethodInfo.cs             All method metadata: namespace, class, params, template, mode, level, eventId, sampleRate, maxPerSecond
    ContainingTypeInfo.cs        Type chain for nested class support
    ParameterInfo.cs             Name, TypeFullName, Kind, IsNullableValueType, IsNullableReferenceType, defaults, RefKind
    ParameterKind.cs             enum: MessageParam, Sink, Exception, CallerFile, CallerLine, CallerMember
    TemplatePart.cs              IsPlaceholder, Text, FormatSpecifier?, BoundParameter?
  Parsing/
    TemplateParser.cs            Parse(template)→parts (with format specifiers), Bind(parts,params)→diagnostics, GenerateTemplateFree(...)
    ParameterClassifier.cs       Classify(IMethodSymbol): Sink(idx0)→Exception→CallerInfo→MessageParam
  Emission/
    MethodEmitter.cs             EmitClassFile(), EmitMethodBody(), EmitSamplingGuard(), EmitRateLimitGuard(), EmitStaticCounterFields()
    TextPathEmitter.cs           writer.Write("..."u8) for literals, WriteFormatted/WriteString for params, :json→SerializeToUtf8Bytes
    StructuredPathEmitter.cs     WriteProperties_<Method>(Utf8JsonWriter, State): JSON property writes, format-aware, :json→Serialize
    NullableEmitter.cs           EmitNullGuard(): HasValue/is not null/direct
    EmbeddedSourceEmitter.cs     Standalone: embeds all Logsmith/*.cs, replaces public→internal
    SerializationKind.cs         enum: Utf8SpanFormattable, SpanFormattable, Formattable, String, ToString
  Diagnostics/
    DiagnosticDescriptors.cs     LSMITH001-007

src/Logsmith.Extensions.Logging/ MEL bridge (net10.0)
  LoggingBuilderExtensions.cs    static: AddLogsmith(this ILoggingBuilder)
  LogsmithLoggerProvider.cs      ILoggerProvider: creates/caches LogsmithLogger per category
  LogsmithLogger.cs              internal ILogger: maps MEL levels→Logsmith, constructs LogEntry, UTF-8 encodes, dispatches
```

## Usage

### Define log methods

```csharp
[LogCategory("MyApp")]
public static partial class Log
{
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

    // Nullable, 'in' ref, format specifiers, :json serialization
    [LogMessage(LogLevel.Warning, "Missing: {key}={value}")]
    public static partial void MissingValue(string key, int? value);
    [LogMessage(LogLevel.Information, "Sensor={reading}")]
    public static partial void SensorData(in SensorReading reading);
    [LogMessage(LogLevel.Information, "Price={price:F2}")]
    public static partial void LogPrice(decimal price);
    [LogMessage(LogLevel.Debug, "Config={config:json}")]
    public static partial void LogConfig(object config);

    // Explicit sink (bypasses LogManager, sink must be first param)
    [LogMessage(LogLevel.Information, "Direct: {msg}")]
    public static partial void DirectLog(ILogSink sink, string msg);

    // Sampling: emit 1-in-100 calls
    [LogMessage(LogLevel.Debug, "Tick={tick}", SampleRate = 100)]
    public static partial void SampledTick(long tick);

    // Rate-limiting: max 10 per second
    [LogMessage(LogLevel.Warning, "Throttled={id}", MaxPerSecond = 10)]
    public static partial void RateLimited(string id);
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
    cfg.SetMinimumLevel("Noisy", LogLevel.Warning);
    cfg.SetMinimumLevel<Log>(LogLevel.Warning);
    cfg.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
    // Dynamic level monitors
    cfg.WatchEnvironmentVariable("LOGSMITH_LEVEL");
    cfg.WatchConfigFile("logsmith.json"); // {MinimumLevel:"Warning", CategoryOverrides:{"Noisy":"Error"}}
});

Log.AppStarted(args.Length);

// Scoped context (async-local, ambient)
using (LogScope.Push("RequestId", requestId))
{
    Log.ProcessingItem(itemId, count); // scope properties available to sinks
}

// Runtime reconfiguration
LogManager.Reconfigure(cfg => { cfg.MinimumLevel = LogLevel.Warning; cfg.AddConsoleSink(); });
```

### MEL bridge

```csharp
builder.Logging.AddLogsmith(); // routes ILogger calls through Logsmith sinks
```

### Standalone mode

```xml
<ItemGroup>
  <ProjectReference Include="Logsmith.Generator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

All runtime types are embedded as `internal` automatically.

## Generator Internals

### Generated code structure (per method)

1. `[Conditional("DEBUG")]` if level ≤ threshold (MSBuild `<LogsmithConditionalLevel>`, default: Debug)
2. Sampling guard: `Interlocked.Increment(ref __sampleCounter_Method) % SampleRate != 0 → return` (if SampleRate > 0)
3. Rate-limit guard: sliding-second window via `__rateWindow_Method`/`__rateCount_Method` with Interlocked (if MaxPerSecond > 0)
4. Early-exit: `if (!LogManager.IsEnabled(level)) return;` (or `!sink.IsEnabled()` for explicit sink)
5. Construct `LogEntry` with UTC ticks, event ID, category, exception, caller info, threadId, threadName
6. `stackalloc byte[128..4096]` → `Utf8LogWriter` → write template parts as UTF-8
7. Dispatch: `LogManager.Dispatch(in entry, utf8Message, state, WriteProperties_Method)` or `sink.Write(in entry, utf8Message)`
8. State ref struct holds message params for structured sink path
9. Static counter fields emitted per-class for sampling/rate-limiting methods

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
| LSMITH007 | Warning | SampleRate and MaxPerSecond both set on same method |

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
- Thread info: `LogEntry.ThreadId`/`ThreadName` captured at call site. Available for structured sinks and `{threadId}`/`{threadName}` template placeholders
- Error handler: `LogConfigBuilder.InternalErrorHandler` — sink exceptions caught in Dispatch, routed to handler or silently swallowed
- Format specifiers: `{value:F2}` for standard .NET format strings, `{obj:json}` for JSON serialization — parsed at compile-time, emitted as static code
- ILogFormatter: pluggable prefix/suffix formatting via `IBufferWriter<byte>`. DefaultLogFormatter (cached category UTF-8) for timestamps/level/category, NullLogFormatter for raw output
- FileSink shared mode: `FileShare.ReadWrite` + named mutex for multi-process append, seek-to-end before write, safe concurrent rolling
- Time-interval rolling: `RollingInterval` enum (None/Hourly/Daily/Weekly/Monthly), uses entry timestamp for testability
- Scoped context: `LogScope` uses `AsyncLocal<LogScope>` linked-list stack; `Push()` returns disposable; `ScopeEnumerator` ref struct for zero-alloc enumeration; Dispatch appends scope to message
- Sampling: compile-time `SampleRate` on `[LogMessage]` → `Interlocked.Increment` modulo guard; zero-overhead when not configured
- Rate-limiting: compile-time `MaxPerSecond` on `[LogMessage]` → sliding-second window with `Interlocked` exchanges; LSMITH007 if combined with SampleRate
- Dynamic levels: `WatchEnvironmentVariable()` polls env var; `WatchConfigFile()` watches JSON file with debounce; both call `LogManager.SetMinimumLevel()`/`SetCategoryOverrides()`
- ThreadBuffer: thread-static `ArrayBufferWriter<byte>` pooling for formatter hot paths, reduces GC pressure
- MEL bridge: `LogsmithLoggerProvider` implements `ILoggerProvider`, `LogsmithLogger` maps MEL→Logsmith levels, constructs LogEntry, UTF-8 encodes via stackalloc, dispatches through LogManager

## Testing

- NUnit only (no FluentAssertions, no Moq)
- `tests/Logsmith.Tests/` — runtime sink behavior, scoping, dynamic levels, sampling, exception handler tests
- `tests/Logsmith.Generator.Tests/` — Roslyn compilation tests via `GeneratorTestHelper`, sampling emission tests
- `tests/Logsmith.Extensions.Logging.Tests/` — MEL bridge integration tests
- `RecordingSink` captures entries in-memory for assertions
- `LogManager.Reset()` (internal) clears state between tests
