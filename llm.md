# Logsmith — LLM Reference

Zero-allocation, source-generator-driven C# logging library. .NET 10+.

## Architecture

Two NuGet packages, two modes:
- **Shared mode**: Reference `Logsmith` (runtime + generator). Generator emits only method bodies.
- **Standalone mode**: Reference only `Logsmith.Generator` as analyzer. Generator embeds all runtime types as `internal`.

Pipeline: `[LogMessage]` partial methods → Roslyn IncrementalGenerator → UTF-8 text path + JSON structured path.

## Project Layout

```
src/Logsmith/                    Runtime library (net10.0)
  LogLevel.cs                    enum byte: Trace=0..Critical=5, None=6
  LogEntry.cs                    readonly struct: Level,EventId,TimestampTicks,Category,Exception?,CallerFile?,CallerLine,CallerMember?
  LogManager.cs                  static: Initialize(Action<LogConfigBuilder>), Reconfigure(...), IsEnabled(LogLevel), Dispatch<TState>(...)
  LogConfigBuilder.cs            Fluent builder: MinimumLevel, AddSink(), AddConsoleSink(), AddFileSink(), AddDebugSink(), SetMinimumLevel(category,level), ClearSinks()
  Utf8LogWriter.cs               ref struct: Write(ROSpan<byte>), WriteFormatted<T:IUtf8SpanFormattable>(in T), WriteString(string?), GetWritten()
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
    DebugSink.cs                 Debugger.IsAttached guard → Debug.WriteLine
    ConsoleSink.cs               extends TextLogSink: ANSI colored, stdout stream, [HH:mm:ss.fff LVL Cat]
    FileSink.cs                  extends BufferedLogSink: async file I/O, 10MB rotation, timestamped rollover
    RecordingSink.cs             List<CapturedEntry> for testing, record CapturedEntry(...)
  Internal/
    SinkSet.cs                   Classify(List<ILogSink>)→TextSinks[]+StructuredSinks[]
    LogConfig.cs                 MinimumLevel, CategoryOverrides, SinkSet

src/Logsmith.Generator/          Source generator (netstandard2.0, Roslyn 4.3)
  LogsmithGenerator.cs           IIncrementalGenerator: syntax predicate→transform→per-class emission
  ModeDetector.cs                IsSharedMode(): checks for "Logsmith" assembly reference
  EventIdGenerator.cs            FNV-1a hash of "Class.Method" or user-specified
  ConditionalCompilation.cs      [Conditional("DEBUG")] for methods ≤ threshold level
  Models/
    LogMethodInfo.cs             All method metadata: namespace, class, params, template, mode, level, eventId
    ParameterInfo.cs             Name, TypeFullName, Kind, IsNullableValueType, IsNullableReferenceType, defaults, RefKind
    ParameterKind.cs             enum: MessageParam, Sink, Exception, CallerFile, CallerLine, CallerMember
    TemplatePart.cs              IsPlaceholder, Text, BoundParameter?
  Parsing/
    TemplateParser.cs            Parse(template)→parts, Bind(parts,params)→diagnostics, GenerateTemplateFree(...)
    ParameterClassifier.cs       Classify(IMethodSymbol): Sink(idx0)→Exception→CallerInfo→MessageParam
  Emission/
    MethodEmitter.cs             EmitClassFile(), EmitMethodBody(): early-exit guard→LogEntry→stackalloc→Utf8LogWriter→Dispatch
    TextPathEmitter.cs           writer.Write("..."u8) for literals, WriteFormatted/WriteString for params
    StructuredPathEmitter.cs     WriteProperties_<Method>(Utf8JsonWriter, State): JSON property writes
    NullableEmitter.cs           EmitNullGuard(): HasValue/is not null/direct
    EmbeddedSourceEmitter.cs     Standalone: embeds all Logsmith/*.cs, replaces public→internal
    SerializationKind.cs         enum: Utf8SpanFormattable, SpanFormattable, Formattable, String, ToString
  Diagnostics/
    DiagnosticDescriptors.cs     LSMITH001-005: unmatched placeholder, unreferenced param, not static partial, no format path, caller info in template
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
    cfg.AddFileSink("app.log");
    cfg.AddDebugSink();
    cfg.SetMinimumLevel("Noisy", LogLevel.Warning); // per-category
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
3. Construct `LogEntry` with UTC ticks, event ID, category, exception, caller info
4. `stackalloc byte[128..4096]` → `Utf8LogWriter` → write template parts as UTF-8
5. Dispatch: `LogManager.Dispatch(in entry, utf8Message, state, WriteProperties_Method)` or `sink.Write(in entry, utf8Message)`
6. State ref struct holds message params for structured sink path

### Buffer sizing

Literals: byte count. String params: +128. Other params: +32. Clamped [128, 4096].

### Diagnostics

| Code | Severity | Description |
|------|----------|-------------|
| LSMITH001 | Error | Placeholder `{x}` has no matching parameter |
| LSMITH002 | Warning | Parameter not referenced in template |
| LSMITH003 | Error | Method must be `static partial` in `partial class` |
| LSMITH004 | Error | Parameter type has no supported formatting path |
| LSMITH005 | Warning | Caller info param name used as template placeholder |

## Key Design Decisions

- UTF-8 first: `Utf8LogWriter` ref struct writes directly to stack buffer, no string intermediary
- Dual dispatch: `SinkSet` classifies sinks once at config time into text[] and structured[] arrays
- Thread safety: `LogManager` uses `volatile` config + `Interlocked.CompareExchange` for init guard
- Sink first-param convention: explicit `ILogSink` parameter must be at index 0, bypasses `LogManager`
- `in` parameter support: `ParameterInfo.RefKind` ("in " or ""), preserved on method signature, state struct ctor, and state construction call
- EventId: user-specified if nonzero, else stable FNV-1a hash of `"ClassName.MethodName"`
- Visibility transform: standalone mode replaces `public` → `internal`, `protected` → `private protected`

## Testing

- NUnit only (no FluentAssertions, no Moq)
- `tests/Logsmith.Tests/` — runtime sink behavior tests
- `tests/Logsmith.Generator.Tests/` — Roslyn compilation tests via `GeneratorTestHelper`
- `RecordingSink` captures entries in-memory for assertions
- `LogManager.Reset()` (internal) clears state between tests
