# Implementation Partition Index

NUM_PLANS = 3

## Plan Assignments

### Plan 1: Core Runtime Library (`src/Logsmith/`)

| # | Spec Section / Feature | Phase |
|---|------------------------|-------|
| 1.1 | Project setup — `Logsmith.csproj`, `net10.0`, zero dependencies | 1 |
| 1.2 | `LogLevel` enum (Trace..None, `byte` backing) | 1 |
| 1.3 | `LogEntry` readonly struct (Level, EventId, TimestampTicks, Category, Exception?, CallerFile?, CallerLine, CallerMember?) | 1 |
| 1.4 | `LogMessageAttribute` (Level, Message, EventId, AlwaysEmit, constructor) | 1 |
| 1.5 | `LogCategoryAttribute` (Name, constructor) | 1 |
| 1.6 | `ILogSink` interface (IsEnabled, Write, IDisposable) | 1 |
| 1.7 | `IStructuredLogSink` interface (WriteStructured\<TState\>, extends ILogSink) | 1 |
| 1.8 | `WriteProperties<TState>` delegate (allows ref struct) | 1 |
| 1.9 | `ILogStructurable` interface (WriteStructured with Utf8JsonWriter) | 1 |
| 1.10 | `Utf8LogWriter` ref struct (ctor, Write, WriteFormatted\<T\>, WriteString, GetWritten, BytesWritten) | 2 |
| 1.11 | Internal `SinkSet` — pre-separated `ILogSink[]` and `IStructuredLogSink[]` arrays, classification at registration time | 2 |
| 1.12 | Immutable internal `LogConfig` object — MinimumLevel, per-category overrides, SinkSet | 2 |
| 1.13 | `LogConfigBuilder` — MinimumLevel property, SetMinimumLevel(category), AddSink, AddConsoleSink, AddFileSink, AddDebugSink, ClearSinks | 3 |
| 1.14 | `LogManager` — Initialize, Reconfigure (Action\<LogConfigBuilder\>), volatile config swap, no locks on read path | 3 |
| 1.15 | `LogManager.IsEnabled(LogLevel)` — volatile read + level check | 3 |
| 1.16 | `LogManager.Dispatch<TState>` — generic with `allows ref struct`, volatile read of SinkSet, iterate text sinks then structured sinks | 3 |

### Plan 2: Source Generator (`src/Logsmith.Generator/`)

| # | Spec Section / Feature | Phase |
|---|------------------------|-------|
| 2.1 | Project setup — `Logsmith.Generator.csproj`, `netstandard2.0`, analyzer output type | 1 |
| 2.2 | `IncrementalGenerator` pipeline — register syntax/semantic providers, combine with MSBuild options | 1 |
| 2.3 | Parameter classification algorithm (Sink, Exception, CallerFile, CallerLine, CallerMember, MessageParam) | 2 |
| 2.4 | Template parsing — extract `{placeholderName}` tokens, case-insensitive matching to MessageParam parameters | 2 |
| 2.5 | Template-free mode — auto-generate `"MethodName param1={param1} param2={param2}"` from method name + MessageParam names | 2 |
| 2.6 | Diagnostics LSMITH001 (placeholder no matching param), LSMITH002 (param not in template), LSMITH003 (must be static partial in partial class), LSMITH005 (caller param name in template) | 2 |
| 2.7 | EventId auto-generation — stable hash of `"ClassName.MethodName"`, user override via attribute property | 3 |
| 2.8 | Conditional compilation — read `LogsmithConditionalLevel` MSBuild property, compare method level ordinal to threshold, emit `[Conditional("DEBUG")]`, AlwaysEmit bypass | 3 |
| 2.9 | Mode detection — `compilation.GetTypeByMetadataName("Logsmith.LogLevel")`, check ContainingAssembly.Name | 3 |
| 2.10 | Embedded source emission — read EmbeddedResource `.cs` files, visibility replacement (`public` → `internal` at type declarations), add to SourceProductionContext | 3 |
| 2.11 | Code emission: text path — Utf8LogWriter usage, type serialization priority chain (IUtf8SpanFormattable > ISpanFormattable > IFormattable > ToString) | 4 |
| 2.12 | Code emission: structured path — Utf8JsonWriter, ILogStructurable detection, WriteProperties delegate emission | 4 |
| 2.13 | Code emission: nullable handling — HasValue guard for `T?` structs, `is not null` guard for reference types, null literal in text/JSON | 4 |
| 2.14 | Code emission: caller info parameters — detect [CallerFilePath]/[CallerLineNumber]/[CallerMemberName], pass to LogEntry, any order/combination | 4 |
| 2.15 | Code emission: explicit sink vs LogManager dispatch — detect ILogSink first param, route accordingly | 4 |
| 2.16 | Diagnostic LSMITH004 (parameter type has no supported formatting path) | 4 |
| 2.17 | Full method body assembly — combine all emission pieces, emit partial method implementation | 5 |

### Plan 3: Sinks, NuGet Packaging & Tests

| # | Spec Section / Feature | Phase |
|---|------------------------|-------|
| 3.1 | `TextLogSink` abstract base class | 1 |
| 3.2 | `BufferedLogSink` abstract base class (async-buffered pattern) | 1 |
| 3.3 | `NullSink` — discards everything | 1 |
| 3.4 | `DebugSink` — System.Diagnostics.Debug output | 1 |
| 3.5 | `ConsoleSink` — ANSI-colored UTF8 to stdout | 2 |
| 3.6 | `FileSink` — Channel\<T\> async-buffered, rolling file support, IAsyncDisposable | 2 |
| 3.7 | `RecordingSink` — List\<CapturedEntry\> for test assertions | 2 |
| 3.8 | NuGet packaging: `Logsmith` package — lib/net10.0/Logsmith.dll + analyzers/dotnet/cs/Logsmith.Generator.dll, transitive bundling | 3 |
| 3.9 | NuGet packaging: `Logsmith.Generator` standalone package — analyzer-only, zero runtime DLLs | 3 |
| 3.10 | Embedded resource build integration — Logsmith .cs files as EmbeddedResource in Logsmith.Generator | 3 |
| 3.11 | `Logsmith.Tests` project setup (NUnit, net10.0) | 4 |
| 3.12 | Runtime behavior tests — LogManager init/reconfig, dispatch, sink routing, level filtering, per-category overrides | 4 |
| 3.13 | Sink tests — each sink type (Console, File, Debug, Recording, Null, buffered base) | 4 |
| 3.14 | `Logsmith.Generator.Tests` project setup (NUnit, net10.0, Roslyn test infrastructure) | 5 |
| 3.15 | Generator compilation tests — parameter classification, template parsing, diagnostics, code emission correctness, standalone mode, conditional compilation | 5 |

## Cross-Plan Dependencies

| Dependency | Reason |
|------------|--------|
| Plan 2, Phase 1 depends on Plan 1, Phase 1 | Generator project references Logsmith types (attributes, LogLevel) for semantic analysis |
| Plan 2, Phase 4 depends on Plan 1, Phase 2 | Code emission references Utf8LogWriter, SinkSet shape, Dispatch signature |
| Plan 3, Phase 1 depends on Plan 1, Phase 1 | Sink base classes implement ILogSink / IStructuredLogSink interfaces |
| Plan 3, Phase 2 depends on Plan 1, Phase 2 | ConsoleSink/FileSink use Utf8LogWriter / LogEntry / SinkSet internals |
| Plan 3, Phase 3 depends on Plan 2, Phase 1 | NuGet packaging bundles the generator assembly, embedded resource config |
| Plan 3, Phase 3 depends on Plan 1, Phase 3 | NuGet packaging bundles the runtime assembly with LogManager |
| Plan 3, Phase 4 depends on Plan 1, Phase 3 | Runtime tests exercise LogManager.Initialize, Dispatch, etc. |
| Plan 3, Phase 4 depends on Plan 3, Phase 2 | Runtime tests exercise sink implementations |
| Plan 3, Phase 5 depends on Plan 2, Phase 5 | Generator tests validate full code emission pipeline |
| Plan 3, Phase 5 depends on Plan 3, Phase 3 | Generator tests may need NuGet/embedded resource setup |
