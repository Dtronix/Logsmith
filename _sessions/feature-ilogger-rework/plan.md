# Implementation Plan: ILogger Rework

This plan implements the Logsmith v2 ILogger API and consolidates the dispatch infrastructure. The new ILogger API supplements (not replaces) the existing `[LogMessage]` pattern. Both APIs share a unified dispatch path through `LoggerContext`.

## Key Concepts

**DispatchInfo**: A `ref struct` that replaces `LogEntry` as the single data carrier through the dispatch path. It carries: level, eventId, timestamp, category, UTF-8 text, UTF-8 JSON, UTF-8 path, tag, exception, caller info, thread info. Being a ref struct, it can hold `ReadOnlySpan<byte>` fields for zero-copy text/JSON/path data.

**LoggerContext**: The central dispatch hub. Holds all state: category, minimum level, sink references (via LogConfig), path node, error handler. Every logging call (both ILogger and [LogMessage]) ultimately dispatches through a `LoggerContext`. LogManager becomes a factory and configuration holder; it no longer dispatches directly.

**PathNode**: Linked-list node for hierarchical paths. Each node has a mutable `Segment` property and a `Parent` pointer. Version-based caching: only rebuilds the formatted UTF-8 path when `CalculateVersionSum()` changes. Thread-safe via `Volatile.Write`/`Read` for segment + `Interlocked.Increment` for version.

**ILogger Interface**: Default interface methods on `ILogger` provide all terminal methods (Debug, Information, etc.) and chain methods (When, Sampled, Tagged, etc.). Only `Context` property must be implemented. `NullLogger` is the sentinel (IsEnabled always false, all operations are no-ops). Chain methods return `ILogger` — either the original, a generated carrier, or `NullLogger`.

**Dual-Buffer Handlers**: Each log level has a `ref struct` handler (e.g., `LogDebugHandler`) that wraps a shared `LogHandlerCore`. The core writes both UTF-8 text and structured JSON simultaneously during `AppendFormatted` calls. `[CallerArgumentExpression]` provides property names for the JSON output. The handler constructor short-circuits via `out bool isEnabled`.

**Carrier Pattern**: For fluent chains like `logger.When(cond).Sampled(100).Tagged("SQL").Debug($"...")`, the generator emits per-chain-shape carrier types (`LogCarrier_N`). The carrier implements `ILogger` (only needs `Context` property — all other methods use default implementations). Thread-static pooled with `_inUse` re-entrancy guard.

**Three Logging Tiers**:
- *Direct*: `logger.Debug($"...")` — handler short-circuits at ~3-5ns when disabled
- *Chained*: `logger.When(cond).Sampled(N).Debug($"...")` — NullLogger propagation at ~8-15ns when disabled
- *Static*: `Log.Trace(logger, $"...")` — `[Conditional]` removes entire call site (0ns)

---

## Phase 1: Core Dispatch Refactor

The foundational change: replace `LogEntry` with `DispatchInfo`, unify the sink interface, update all sinks and formatters, remove `LogScope` and `IStructuredLogSink`.

**Dependencies**: None (first phase).

### New files
- `src/Logsmith/DispatchInfo.cs` ��� `public ref struct DispatchInfo` with fields: `LogLevel Level`, `int EventId`, `long TimestampTicks`, `string Category`, `ReadOnlySpan<byte> Utf8Message`, `ReadOnlySpan<byte> Utf8Json`, `ReadOnlySpan<byte> Utf8Path`, `Exception? Exception`, `string? Tag`, `string? CallerFile`, `int CallerLine`, `string? CallerMember`, `int ThreadId`, `string? ThreadName`.

### Deleted files
- `src/Logsmith/LogEntry.cs` — replaced by DispatchInfo
- `src/Logsmith/LogScope.cs` — removed (explicit scoping via ILogger.Scoped() in later phase)
- `src/Logsmith/Sinks/IStructuredLogSink.cs` — merged into ILogSink
- `src/Logsmith/Sinks/WriteProperties.cs` — no longer needed (pre-built JSON)

### Modified files

**ILogSink.cs**: Change `Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)` → `Write(in DispatchInfo info)`. DispatchInfo already carries the UTF-8 text and JSON, so no separate message parameter is needed.

**IFlushableLogSink.cs**: Unchanged (still extends ILogSink).

**ILogStructurable.cs**: Keep for now — types can still implement this for custom JSON serialization.

**SinkSet.cs**: Remove `TextSinks`/`StructuredSinks` split. Single `ILogSink[] Sinks` array. `Classify()` simplifies to just collecting all sinks. Keep `AllSinks` for disposal.

**LogConfig.cs**: Update constructor to use simplified SinkSet.

**LogManager.cs**: Update `Dispatch` signature. Remove the `LogScope.Current` check and scope augmentation. The new method creates a `DispatchInfo` from parameters and dispatches to sinks. Signature becomes: `Dispatch(in DispatchInfo info)` — a simpler method that just iterates sinks. The old generic `Dispatch<TState>` is removed. For backward compat with the current generator output during this phase, add a bridge method that takes the old parameters and constructs a DispatchInfo.

**TextLogSink.cs**: Update `Write` and `WriteMessage` signatures to use `in DispatchInfo`.

**ConsoleSink.cs**: Update `WriteMessage` to use DispatchInfo. Extract Level for coloring, pass DispatchInfo to formatter.

**BufferedLogSink.cs**: Update `BufferedEntry` record struct to store all DispatchInfo fields (non-span fields directly, span data copied to `byte[]` arrays). `Write(in DispatchInfo)` copies the spans and enqueues. `WriteBufferedAsync` receives BufferedEntry. Add helper method `BufferedEntry.ToDispatchInfo()` that reconstructs a DispatchInfo from the stored data (for passing to formatter).

**FileSink.cs**: Update `WriteBufferedAsync` — reconstruct DispatchInfo from BufferedEntry for formatter calls.

**StreamSink.cs**: Same pattern as FileSink.

**DebugSink.cs**: Update `Write` to use `in DispatchInfo`.

**NullSink.cs**: Update `Write` signature.

**RecordingSink.cs**: Update `Write` and `CapturedEntry` record. Add `Tag`, `Path` (as string, decoded from UTF-8), `JsonMessage` (as string, decoded from UTF-8) fields to CapturedEntry.

**ILogFormatter.cs**: Change to `FormatPrefix(in DispatchInfo info, IBufferWriter<byte> output)` and `FormatSuffix(in DispatchInfo info, IBufferWriter<byte> output)`.

**DefaultLogFormatter.cs**: Update to use DispatchInfo fields. Add path rendering after category: `Category|Path` when path is present. Add tag rendering: `#Tag` before closing bracket. New format: `[HH:mm:ss.fff LVL Category|Path #Tag] message`.

**LogConfigBuilder.cs**: Remove `AddSink` overload that used IStructuredLogSink typing if any. Simplify sink collection.

**Generator MethodEmitter.cs**: Update emitted code to construct a `DispatchInfo` instead of `LogEntry`. Remove state struct + WriteProperties emission for structured dispatch. The emitted code builds UTF-8 text via Utf8LogWriter (as before) and also builds UTF-8 JSON via a Utf8JsonWriter, then passes both in DispatchInfo. Update `EmitStandardMethodBody` and `EmitAbstractionMethodBody`.

**Generator StructuredPathEmitter.cs**: Refactor to emit code that writes JSON into a buffer during the method body (inline), rather than as a separate WriteProperties callback. The JSON bytes go into the DispatchInfo.

**Extensions.Logging LogsmithLogger.cs**: Update `Log<TState>` to construct DispatchInfo instead of LogEntry. Remove BeginScope/LogScope usage (return null or a no-op disposable from BeginScope).

**All test files**: Update to use DispatchInfo instead of LogEntry. Remove LogScope tests. Update RecordingSink assertions for new CapturedEntry shape.

### Tests to add/modify
- Update all sink tests to construct DispatchInfo and call Write
- Update formatter tests for new format with path and tag
- Remove LogScope-specific tests
- Add tests verifying JSON bytes are passed through to RecordingSink
- Generator tests updated for new emitted code patterns

---

## Phase 2: LoggerContext + PathNode + LogManager Refactor

Introduce `LoggerContext` as the central dispatch hub. LogManager becomes a factory. PathNode provides hierarchical path support.

**Dependencies**: Phase 1 (DispatchInfo and unified sink interface must exist).

### New files
- `src/Logsmith/LoggerContext.cs` — Class holding: `string Category`, `PathNode? PathNode`, `LoggerContext? Parent`, `LogLevel MinimumLevel` (override, defaults to parent/config level). Methods:
  - `bool IsEnabled(LogLevel level)` — checks category overrides, then minimum level
  - `void Dispatch(in DispatchInfo info)` — iterates sinks, calls Write, handles errors
  - `LoggerContext CreateChild(string? segment)` — creates child context with parent link, shared sinks
  - References `LogConfig` (volatile, from LogManager) for sinks + error handler + level

- `src/Logsmith/Internal/PathNode.cs` — Class with: `string? Segment` (volatile read/write), `int Version` (interlocked increment), `PathNode? Parent`. Methods:
  - `int CalculateVersionSum()` — walks chain, sums versions (volatile reads)
  - `int WriteUtf8Path(Span<byte> destination)` — writes `Segment1|Segment2|...` as UTF-8 directly to buffer, returns bytes written. Uses a ref struct enumerator to walk root-to-leaf.
  - Version-based caching: `LoggerContext` caches the last-built UTF-8 path bytes and version sum. Rebuilds only when version sum changes.

### Modified files

**LogManager.cs**: 
- Add `GetLogger(string category)` → creates/returns a `LoggerContext` for the given category.
- Add `GetLogger<T>()` → returns `LoggerContext` with `typeof(T).Name` as category.
- The old `Dispatch` method becomes a thin wrapper: constructs DispatchInfo (as before) and delegates to a shared internal dispatch path that iterates sinks. Eventually, `[LogMessage]` generated code will call `LoggerContext.Dispatch()` directly (Phase 5), but for now the existing generated code still calls `LogManager.Dispatch()`.
- Store created LoggerContexts for reference-equality on GetLogger (ConcurrentDictionary keyed by category).
- When `Reconfigure()` is called, all LoggerContexts automatically see the new config (they reference LogManager's volatile `_config`).

**LogConfig.cs**: No changes needed — LoggerContext reads `LogManager._config` directly.

### Tests to add
- LoggerContext dispatch tests (create context, dispatch, verify sink receives DispatchInfo)
- LoggerContext.IsEnabled tests (minimum level, category overrides)
- LoggerContext.CreateChild tests (parent link, shared config, independent level override)
- PathNode tests (segment mutation, version tracking, UTF-8 path building, caching)
- PathNode thread-safety tests (concurrent segment mutation + path reading)
- LogManager.GetLogger / GetLogger<T> tests

---

## Phase 3: ILogger Interface + Supporting Types

The user-facing ILogger API surface: interface with default implementations, NullLogger, LogScope struct, TimingOperation struct.

**Dependencies**: Phase 2 (LoggerContext must exist for ILogger.Context property).

### New files
- `src/Logsmith/ILogger.cs` — Interface with `LoggerContext Context { get; }` and default implementations for:
  - Terminal methods: `Debug(...)`, `Trace(...)`, `Information(...)`, `Warning(...)`, `Error(...)`, `Critical(...)` — each with 4 overloads (handler, handler+exception, string, string+exception). Default bodies dispatch through `Context.Dispatch()`.
  - Chain methods: `When(bool condition)`, `Sampled(int rate)`, `RateLimited(int maxPerSecond)`, `Tagged(string tag)` — defaults return `this` (or `NullLogger.Instance` for `When(false)`).
  - Hierarchy: `CreateChild(string? segment)`, `PathSegment { get; set; }` — default delegates to Context.
  - `DPanic(...)` — dispatches at Error level, throws `InvalidOperationException` in DEBUG builds.
  - `IsEnabled(LogLevel)` — default delegates to Context.

- `src/Logsmith/ILoggerOfT.cs` — `ILogger<T> : ILogger` (marker interface, category from `typeof(T).Name`).

- `src/Logsmith/NullLogger.cs` — Singleton implementing ILogger. Has a sentinel `LoggerContext` with `MinimumLevel = LogLevel.None` (IsEnabled always false). `PathSegment` setter is no-op. `CreateChild` returns `NullLogger.Instance`.

- `src/Logsmith/LogScope.cs` (NEW — different from the deleted AsyncLocal one) — `public struct LogScope : ILogger, IDisposable`. Wraps a `LoggerContext` with a pushed path segment. `Dispose()` pops the path segment. Extension method `Scoped(this ILogger, string segment)` creates it.

- `src/Logsmith/TimingOperation.cs` — `public struct TimingOperation : ILogger, IDisposable`. Wraps a `LoggerContext` with start timestamp + operation ID. Methods: `Complete()`, `Fail(Exception)`, `TimeStep(string)`, `Dispose()` (logs "abandoned" if neither Complete nor Fail called). Extension method `TimeOperation(this ILogger, string name)` creates it.

- `src/Logsmith/LoggerExtensions.cs` — Extension methods: `Scoped()`, `TimeOperation()`. Placed here to avoid boxing (return concrete struct types, not ILogger).

### Modified files

**LogManager.cs**: `GetLogger()` now returns an `ILogger` (a simple wrapper implementing ILogger that holds a LoggerContext). Add an internal class `LoggerInstance : ILogger` that implements `ILogger` by wrapping `LoggerContext`.

### Tests to add
- ILogger default method tests (all terminal methods dispatch correctly)
- ILogger chain tests (When, Sampled, RateLimited, Tagged — defaults)
- NullLogger tests (singleton, all no-ops, IsEnabled false, propagation through chains)
- LogScope struct tests (scoped path segment appears in dispatch, dispose pops it)
- TimingOperation tests (start/complete/fail/abandon lifecycle, correlation IDs)
- CreateChild tests (hierarchy, path segment propagation)
- PathSegment mutation tests (mutable segments update all child loggers)

---

## Phase 4: InterpolatedStringHandler Infrastructure

The dual-buffer handler system: `LogHandlerCore` and per-level wrappers. This enables `logger.Debug($"...")` with structured JSON + UTF-8 text simultaneously.

**Dependencies**: Phase 3 (ILogger interface must exist for handler constructor parameter).

### New files
- `src/Logsmith/Handlers/LogHandlerCore.cs` — `ref struct` with:
  - Fields: `ArrayBufferWriter<byte> _textBuffer`, `Utf8JsonWriter _jsonWriter`, `ArrayBufferWriter<byte> _jsonBuffer`, `bool _enabled`, `bool _jsonFinalized`, `LoggerContext _context`, `LogLevel _level`, `Exception? _exception`.
  - Constructor: `(int literalLength, int formattedCount, ILogger logger, LogLevel level, out bool isEnabled, Exception? ex = null)` — checks `logger.IsEnabled(level)`, rents buffers from ThreadBuffer pool if enabled.
  - `AppendLiteral(string s)` — UTF-8 encodes to text buffer only (literals are not structured properties).
  - `AppendFormatted<T>(T value, string? name)` — writes value to text buffer (UTF-8) AND writes JSON property to JSON writer. Property name from `[CallerArgumentExpression]`. Uses JIT-specialized `typeof(T) == typeof(int)` + `Unsafe.As` pattern for zero-boxing.
  - `AppendFormatted<T>(T value, string? format, string? name)` — with format specifier.
  - `AppendFormatted(string? value, string? name)` — string specialization.
  - `bool IsEnabled` property.
  - `ReadOnlySpan<byte> GetTextWritten()` — returns text buffer contents.
  - `ReadOnlySpan<byte> GetJsonWritten()` — finalizes JSON (writes end object, sets `_jsonFinalized` guard), returns JSON buffer contents.

- `src/Logsmith/Handlers/LogDebugHandler.cs` (and Trace, Information, Warning, Error, Critical) — Each is a `[InterpolatedStringHandler] ref struct` that wraps `LogHandlerCore`:
  ```csharp
  [InterpolatedStringHandler]
  public ref struct LogDebugHandler
  {
      private LogHandlerCore _core;
      public LogDebugHandler(int literalLength, int formattedCount,
          ILogger logger, out bool isEnabled)
          => _core = new(literalLength, formattedCount, logger, LogLevel.Debug, out isEnabled);
      public LogDebugHandler(int literalLength, int formattedCount,
          ILogger logger, Exception? ex, out bool isEnabled)
          => _core = new(literalLength, formattedCount, logger, LogLevel.Debug, out isEnabled, ex);
      public void AppendLiteral(string s) => _core.AppendLiteral(s);
      public void AppendFormatted<T>(T value,
          [CallerArgumentExpression(nameof(value))] string? name = null)
          => _core.AppendFormatted(value, name);
      // ... other AppendFormatted overloads
      public bool IsEnabled => _core.IsEnabled;
      public ReadOnlySpan<byte> GetTextWritten() => _core.GetTextWritten();
      public ReadOnlySpan<byte> GetJsonWritten() => _core.GetJsonWritten();
  }
  ```

### Modified files

**ILogger.cs**: Add terminal method bodies that use the handlers. The default implementations call `Context.Dispatch()` with a `DispatchInfo` built from handler output:
```csharp
void Debug([InterpolatedStringHandlerArgument("")] ref LogDebugHandler handler)
{
    if (!handler.IsEnabled) return;
    Context.Dispatch(new DispatchInfo
    {
        Level = LogLevel.Debug,
        Utf8Message = handler.GetTextWritten(),
        Utf8Json = handler.GetJsonWritten(),
        // ... timestamp, category, thread info filled by Context.Dispatch
    });
}
```

Actually — `Context.Dispatch` should fill in timestamp, category, thread info, and path automatically (from the context's own state). So the ILogger default methods only need to pass level, text, json, exception, tag.

**LoggerContext.cs**: Update `Dispatch` to fill in timestamp, category, path (from PathNode), thread info before dispatching to sinks. The caller provides level, text, json, exception, tag, caller info. Context provides: category, path, timestamp, threadId, threadName, eventId (or caller passes it).

### Tests to add
- Handler dual-buffer tests (text + JSON output correct for various types)
- Handler short-circuit tests (disabled logger → handler does no work)
- Handler CallerArgumentExpression tests (property names correct)
- JIT-specialized JSON write tests (int, double, bool, string, DateTime, Guid)
- Handler GetJsonWritten idempotency test (_jsonFinalized guard)
- End-to-end: `logger.Debug($"User {userId} processed {count} items")` → verify RecordingSink gets correct text and JSON
- Nullable handling tests
- Format specifier tests

---

## Phase 5: Static Log Class + Generator Adaptation

The static `Log` class for `[Conditional]` stripping. Update the existing `[LogMessage]` generator to dispatch through `LoggerContext`.

**Dependencies**: Phase 4 (handlers must exist for Static tier; LoggerContext must exist for generator update).

### New files
- `src/Logsmith/Log.cs` — Static class with `[Conditional]` methods:
  ```csharp
  public static class Log
  {
      [Conditional("LOGSMITH_TRACE")]
      public static void Trace(ILogger logger,
          [InterpolatedStringHandlerArgument("logger")] ref LogTraceHandler handler) { ... }
      [Conditional("LOGSMITH_DEBUG")]
      public static void Debug(ILogger logger,
          [InterpolatedStringHandlerArgument("logger")] ref LogDebugHandler handler) { ... }
      // Information, Warning, Error, Critical — no [Conditional], always present
      public static void Information(ILogger logger,
          [InterpolatedStringHandlerArgument("logger")] ref LogInformationHandler handler) { ... }
      // ... etc.
  }
  ```

### Modified files

**Generator MethodEmitter.cs**: Major update. Emitted [LogMessage] method bodies now:
1. Get a LoggerContext — either from the optional ILogger parameter or from a static field on the class.
2. Build UTF-8 text via Utf8LogWriter (existing pattern).
3. Build UTF-8 JSON inline via Utf8JsonWriter (replaces state struct + WriteProperties).
4. Construct DispatchInfo with text + JSON + caller info.
5. Call `LoggerContext.Dispatch(in info)`.

The generator emits a `private static LoggerContext? __loggerContext;` field on each partial class, lazily initialized from `LogManager.GetLogger("CategoryName").Context`.

When the method has an ILogger parameter, it uses that logger's Context instead of the static field.

**Generator StructuredPathEmitter.cs**: No longer emits WriteProperties methods. Instead, emits inline Utf8JsonWriter code within the method body.

**Generator MethodEmitter.cs** (state structs): Remove state struct emission — JSON is built inline.

**Generator EmbeddedSourceEmitter.cs**: Add new types to embedded source list: DispatchInfo, LoggerContext, PathNode, ILogger (and related), handlers, NullLogger, Log class. For standalone mode, these are emitted as internal. For abstraction mode, ILogger/ILogger<T> are public.

**Generator DiagnosticDescriptors.cs**: No new diagnostics needed for this phase.

### Tests to add
- Static Log class: [Conditional] stripping tests (verify Trace/Debug calls removed when symbols not defined)
- Generator tests: verify emitted code uses DispatchInfo, Utf8JsonWriter inline, LoggerContext dispatch
- Generator tests: verify static LoggerContext field emitted on partial class
- Generator tests: verify optional ILogger parameter handling

---

## Phase 6: Generator Stage 2 — Interceptors + Chain Carriers

The interceptor-based code generation for ILogger API calls. This is the "magic" that provides per-call-site optimizations.

**Dependencies**: Phase 5 (ILogger, handlers, and base generator infrastructure must exist).

### New files
- Generator: `src/Logsmith.Generator/Interception/InterceptorAnalyzer.cs` — Analyzes the compilation for ILogger terminal and chain calls. Uses `SyntaxProvider` to find invocation expressions, then `SemanticModel` to confirm ILogger receiver type. Walks syntax tree to detect chain shapes.

- Generator: `src/Logsmith.Generator/Interception/ChainAnalyzer.cs` — Walks from terminal call backwards through MemberAccessExpression chain. Collects: method names, arguments (constant vs runtime), chain shape identifier. Validates chains are continuous (LSMITH013 diagnostic).

- Generator: `src/Logsmith.Generator/Interception/InterceptorEmitter.cs` — Emits interceptor methods for each call site:
  - **Direct calls** (`logger.Debug($"...")`): Interceptor fills in caller file/line/member, computes event ID (FNV-1a of template literals), passes to default implementation.
  - **Chain first-call interceptors**: Creates carrier, does all early-out checks (condition, level, sampling), returns carrier or NullLogger.
  - **Chain intermediate interceptors**: Stuffs runtime values into carrier fields (constants already baked into first/terminal). No-ops for constant-folded values.
  - **Chain terminal interceptors**: Dispatches with all accumulated state from carrier, releases carrier.

- Generator: `src/Logsmith.Generator/Interception/CarrierEmitter.cs` — Emits per-chain-shape carrier types:
  ```csharp
  internal class LogCarrier_N : ILogger
  {
      internal LoggerContext Context;
      internal bool _inUse;
      // Fields for runtime chain values (tags, etc.)
      LoggerContext ILogger.Context => Context;
  }
  ```
  Thread-static pooled. `_inUse` re-entrancy guard.

- Generator: `src/Logsmith.Generator/Interception/InterceptsLocationEmitter.cs` — Emits the `InterceptsLocationAttribute` as a `file class` in `System.Runtime.CompilerServices`. Uses `SemanticModel.GetInterceptableLocation()` for targeting.

### Modified files

**LogsmithGenerator.cs**: Add second pipeline using `RegisterImplementationSourceOutput` (Stage 2). This stage:
1. Collects all ILogger invocations via syntax predicate (looks for method calls on ILogger-typed expressions).
2. Extracts chain information via semantic model.
3. Groups by chain shape.
4. Emits interceptors, carriers, sampling counters, event IDs.
5. Emits the `InterceptsLocationAttribute` definition.

**Generator .csproj**: Ensure Roslyn 4.12.0+ dependency for `GetInterceptableLocation` API. Add `<InterceptorsNamespaces>` MSBuild property documentation.

### New diagnostics
- `LSMITH013`: Chain broken by variable (intermediate result stored). The generator detects this when a chain call's parent in the syntax tree is not a `MemberAccessExpressionSyntax`.

### Tests to add
- Interceptor generation tests: verify interceptors emitted for direct ILogger calls
- Interceptor generation tests: verify chain detection and carrier emission
- Interceptor generation tests: verify event ID computation (FNV-1a of template literals)
- Interceptor generation tests: verify caller info embedding
- Interceptor generation tests: verify sampling counter emission for Sampled() chains
- LSMITH013 diagnostic tests
- End-to-end compilation tests: user code with ILogger calls compiles and runs correctly with interceptors

---

## Phase 7: DI Integration + Extensions.Logging + Cleanup

Final integration: DI support, MEL bridge update, comprehensive testing, cleanup.

**Dependencies**: Phase 6 (all core functionality must exist).

### New files
- `src/Logsmith/DependencyInjection/LogsmithServiceCollectionExtensions.cs` — Extension method `AddLogsmith(this IServiceCollection, Action<LogConfigBuilder>)`. Registers: `ILogger` (singleton via LogManager), `ILogger<T>` (open generic, each resolved via `LogManager.GetLogger<T>()`).

### Modified files

**Extensions.Logging LogsmithLogger.cs**: Update to use LoggerContext/DispatchInfo. `BeginScope<TState>` returns a no-op disposable (LogScope removed). Or, better: implement BeginScope using the new explicit scoping if there's a way to bridge MEL's ambient scope model to the explicit model. Simplest: return a no-op disposable and document that MEL scoping is not supported (use ILogger.Scoped() instead).

**Extensions.Logging LogsmithLoggerProvider.cs**: May need minor updates for new types.

**Extensions.Logging tests**: Update for new behavior.

### Cleanup
- Remove any remaining references to old LogEntry type
- Remove any remaining references to LogScope (AsyncLocal version)
- Remove any remaining references to IStructuredLogSink / WriteProperties
- Verify all `prototype/` references are documentation-only (no production code depends on prototypes)
- Verify standalone/abstraction modes work with all new types
- Verify embedded source emission includes all new types

### Tests to add
- DI integration tests (resolve ILogger, ILogger<T> from service provider)
- End-to-end integration tests: full pipeline from ILogger.Debug($"...") through interceptor through dispatch through sink
- End-to-end: chain calls with interceptors
- End-to-end: [LogMessage] coexisting with ILogger on same sink
- Performance sanity tests (verify disabled cost is in expected range)
- Standalone mode compilation test with new types
- Abstraction mode compilation test with new types

---

## Phase Summary

| Phase | Description | Key Deliverable |
|-------|-------------|-----------------|
| 1 | Core Dispatch Refactor | DispatchInfo, unified ILogSink, all sinks updated |
| 2 | LoggerContext + PathNode | Central dispatch hub, hierarchical paths |
| 3 | ILogger Interface + Types | ILogger, NullLogger, LogScope struct, TimingOperation |
| 4 | Handlers | Dual-buffer InterpolatedStringHandlers |
| 5 | Static Log + Generator | [Conditional] Log class, [LogMessage] generator adaptation |
| 6 | Interceptors | Stage 2 generator, chain carriers, per-call-site optimizations |
| 7 | DI + MEL + Cleanup | Service collection integration, Extensions.Logging bridge |
