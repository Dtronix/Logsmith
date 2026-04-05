# Work Handoff: feature-ilogger-rework

## Key Components
- **DispatchInfo** (`src/Logsmith/DispatchInfo.cs`): Ref struct carrying Level, EventId, TimestampTicks, Category, Utf8Message, Utf8Json, Utf8Path, Exception, Tag, CallerFile/Line/Member, ThreadId/ThreadName.
- **ILogSink** (`src/Logsmith/Sinks/ILogSink.cs`): Unified sink interface with `Write(in DispatchInfo)`.
- **LogManager** (`src/Logsmith/LogManager.cs`): Factory + config holder. `GetLogger()`/`GetLogger<T>()` returns ILogger. `Dispatch()` iterates sinks.
- **LoggerContext** (`src/Logsmith/LoggerContext.cs`): Central dispatch hub. Fills in Category, TimestampTicks, ThreadId, ThreadName, Utf8Path. Holds PathNode for hierarchical paths. Version-based UTF-8 path caching.
- **PathNode** (`src/Logsmith/Internal/PathNode.cs`): Linked-list path nodes with volatile segment mutation and interlocked version tracking.
- **ILogger** (`src/Logsmith/ILogger.cs`): Interface with default implementations for terminal methods (string + handler overloads), chain methods (When/Sampled/Tagged), hierarchy (CreateChild/PathSegment).
- **LoggerInstance** (`src/Logsmith/LoggerInstance.cs`): Simple ILogger wrapper around LoggerContext.
- **NullLogger** (`src/Logsmith/NullLogger.cs`): Singleton with IsEnabled=false, CreateChild returns self.
- **LogScope** (`src/Logsmith/LogScope.cs`): Struct-based ILogger+IDisposable, pushes path segment on create, clears on dispose.
- **TimingOperation** (`src/Logsmith/TimingOperation.cs`): Struct-based timed operation with Complete/Fail/TimeStep/abandon-on-dispose.
- **LogHandlerCore** (`src/Logsmith/Handlers/LogHandlerCore.cs`): Ref struct dual-buffer (UTF-8 text + JSON). JIT-specialized WriteJsonProperty for zero-boxing.
- **LogHandlers** (`src/Logsmith/Handlers/LogHandlers.cs`): Six per-level InterpolatedStringHandler types wrapping LogHandlerCore.
- **Log** (`src/Logsmith/Log.cs`): Static class with [Conditional] Trace/Debug methods for 0ns compile-time stripping.
- **Generator MethodEmitter** (`src/Logsmith.Generator/Emission/MethodEmitter.cs`): Emits static `__loggerContext` field per class, dispatches through LoggerContext, inline Utf8JsonWriter for JSON.

## Completions (This Session)
- **Phase 2: LoggerContext + PathNode** — Committed as `eea2d6d`
  - Created LoggerContext (central dispatch hub)
  - Created PathNode (hierarchical paths with version-based caching)
  - LogManager gains GetLogger()/GetLogger<T>() with ConcurrentDictionary caching
  - 39 new tests (PathNode thread safety, context dispatch, path rendering)

- **Phase 3: ILogger Interface + Types** — Committed as `dea0797`
  - ILogger with default implementations for all terminal/chain/hierarchy methods
  - ILogger<T> marker interface
  - LoggerInstance, NullLogger, LogScope, TimingOperation, LoggerExtensions
  - LogManager.GetLogger() now returns ILogger
  - Resolved ILogger naming conflict with MEL using aliases/qualification
  - 45 new tests

- **Phase 4: InterpolatedStringHandler Infrastructure** — Committed as `56a58bd`
  - LogHandlerCore ref struct with dual text+JSON buffers
  - Six per-level handler types with short-circuit constructors
  - ILogger gains handler-based overloads via [InterpolatedStringHandlerArgument]
  - 22 new tests (dual-buffer, short-circuit, JSON types, CallerArgumentExpression)

- **Phase 5: Static Log Class + Generator Adaptation** — Committed as `b5c3023`
  - Static Log class with [Conditional] methods
  - Generator emits static __loggerContext field, dispatches through context
  - Generator emits inline Utf8JsonWriter for structured JSON output
  - DispatchInfo simplified (context fills timestamp/thread/category)
  - Updated 4 generator tests for new dispatch pattern

## Previous Session Completions
- Design document and prototypes (5 commits pre-existing on branch)
- Phase 1: Core Dispatch Refactor (committed as `67a77f3`)

## Progress
- Phase 5/7 complete
- All 366 tests green (252 runtime + 106 generator + 8 MEL bridge)
- Build succeeds across entire solution (including samples and benchmarks)

## Current State
- Working tree is clean (committed)
- On branch `feature/ilogger-rework`
- No WIP changes

## Known Issues / Bugs
- Naming conflict: `Logsmith.Log` (static class) vs user-defined `Log` classes. Samples use `using Log = Namespace.Log;` alias. This is a known trade-off — same pattern as Serilog.Log.
- ScopedContextBenchmark._logsmithScope is always null (LogScope not yet wired into benchmarks)
- Chain methods (Sampled/RateLimited/Tagged) are stubs that return `this` — only functional with interceptors (Phase 6)

## Dependencies / Blockers
None. Phase 6 can proceed immediately.

## Architecture Decisions
- **ILogger default interface methods**: All terminal/chain/hierarchy methods are default implementations. Only `Context` property is abstract. This means struct types (LogScope, TimingOperation) must be cast to `ILogger` to call default methods — boxing is acceptable for these types since they do I/O.
- **LoggerContext fills context fields**: When dispatching through LoggerContext, it fills Category, TimestampTicks, ThreadId, ThreadName, Utf8Path. Callers only provide Level, EventId, Utf8Message, Utf8Json, Exception, CallerInfo.
- **Generator dispatches through LoggerContext**: Standard mode uses `__ctx.Dispatch()`. Explicit sink mode retains direct `sink.Write()` with full DispatchInfo. Abstraction mode retains `__logger.Write()`.
- **Inline JSON in generator**: Instead of WriteProperties callback, generator emits inline Utf8JsonWriter code for each message parameter with type-aware writes (WriteNumber for int/double, WriteBoolean for bool, WriteString for string/DateTime/Guid).

## Open Questions
None. All design decisions resolved in DESIGN phase.

## Next Work (Priority Order)
1. **Phase 6: Interceptors** — Generator Stage 2 with RegisterImplementationSourceOutput. InterceptorAnalyzer, ChainAnalyzer, InterceptorEmitter, CarrierEmitter, InterceptsLocationEmitter. This enables chain methods to actually work (Tagged, Sampled, etc.) and embeds caller info.
2. **Phase 7: DI + MEL + Cleanup** — ServiceCollection integration, MEL bridge update, comprehensive cleanup, end-to-end integration tests.
