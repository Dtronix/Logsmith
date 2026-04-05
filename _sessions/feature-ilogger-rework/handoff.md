# Work Handoff: feature-ilogger-rework

## Key Components
- **DispatchInfo** (`src/Logsmith/DispatchInfo.cs`): Ref struct replacing LogEntry. Carries Level, EventId, TimestampTicks, Category, Utf8Message, Utf8Json, Utf8Path, Exception, Tag, CallerFile/Line/Member, ThreadId/ThreadName.
- **ILogSink** (`src/Logsmith/Sinks/ILogSink.cs`): Unified sink interface with `Write(in DispatchInfo)`. IStructuredLogSink removed.
- **LogManager** (`src/Logsmith/LogManager.cs`): Currently still the dispatch hub via `Dispatch(in DispatchInfo)`. Phase 2 moves dispatch to LoggerContext.
- **SinkSet** (`src/Logsmith/Internal/SinkSet.cs`): Simplified to single `ILogSink[] Sinks` array (no text/structured split).
- **DefaultLogFormatter** (`src/Logsmith/Formatting/DefaultLogFormatter.cs`): Updated for `[time LVL Category|Path #Tag]` format.
- **BufferedLogSink**: BufferedEntry now stores all DispatchInfo fields with combined byte[] buffer for message+json+path. Has `ToDispatchInfo()` reconstruction method.
- **RecordingSink**: CapturedEntry now has Tag, JsonMessage, Path fields.
- **Generator MethodEmitter**: Emits DispatchInfo construction and `LogManager.Dispatch(in __info)`. State structs and WriteProperties removed.

## Completions (This Session)
- **Phase 1: Core Dispatch Refactor** — Committed as `67a77f3`
  - Created DispatchInfo ref struct
  - Updated ILogSink to `Write(in DispatchInfo)`
  - Removed: LogEntry, LogScope (AsyncLocal), IStructuredLogSink, WriteProperties
  - Updated all 7 sink implementations (Console, File, Stream, Buffered, Debug, Null, Recording)
  - Updated ILogFormatter and DefaultLogFormatter with path/tag support
  - Simplified SinkSet, updated LogManager.Dispatch
  - Updated generator MethodEmitter for DispatchInfo
  - Updated Extensions.Logging bridge (BeginScope returns no-op)
  - Updated all samples, benchmarks, and tests
  - 260 tests passing (146 runtime + 106 generator + 8 MEL bridge)

## Previous Session Completions
- Design document and prototypes (5 commits pre-existing on branch)

## Progress
- Phase 1/7 complete
- All 260 tests green
- Build succeeds across entire solution

## Current State
- Working tree is clean (committed)
- On branch `feature/ilogger-rework`
- No WIP changes

## Known Issues / Bugs
- Utf8Json field in DispatchInfo is always empty for [LogMessage] generated code (JSON emission deferred to Phase 4/5 when handler infrastructure exists)
- ScopedContextBenchmark._logsmithScope is always null (LogScope removed, explicit scoping not yet implemented)

## Dependencies / Blockers
None. Phase 2 can proceed immediately.

## Architecture Decisions
- **LoggerContext as central dispatch hub** — LogManager becomes factory/config holder. Both [LogMessage] and new ILogger API will dispatch through LoggerContext. (Decision, not yet implemented — Phase 2)
- **Pre-built JSON bytes** — Sinks receive UTF-8 JSON in DispatchInfo. No more WriteProperties callback. (Infrastructure ready, JSON emission pending)
- **No AsyncLocal scoping** — LogScope removed. Explicit struct-based scoping via ILogger.Scoped() coming in Phase 3.
- **Caller info via interceptors** — Will be baked into generated code at compile time (Phase 6).

## Open Questions
None. All design decisions resolved in DESIGN phase.

## Next Work (Priority Order)
1. **Phase 2: LoggerContext + PathNode** — Create LoggerContext class (central dispatch), PathNode for hierarchical paths, update LogManager as factory with GetLogger()/GetLogger<T>()
2. **Phase 3: ILogger Interface + Types** — ILogger with default implementations, ILogger<T>, NullLogger, LogScope struct, TimingOperation struct
3. **Phase 4: Handlers** — LogHandlerCore ref struct, per-level InterpolatedStringHandlers
4. **Phase 5: Static Log + Generator** — [Conditional] Log class, [LogMessage] generator adaptation for LoggerContext
5. **Phase 6: Interceptors** — Generator Stage 2 with RegisterImplementationSourceOutput, chain carriers
6. **Phase 7: DI + MEL + Cleanup** — ServiceCollection integration, MEL bridge update
