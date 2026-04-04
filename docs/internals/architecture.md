# Architecture

## Package structure

The `Logsmith` NuGet package contains the runtime library in `lib/net10.0/` and the source generator in `analyzers/dotnet/cs/`. Referencing `Logsmith` provides both.

The `Logsmith.Generator` NuGet package is a thin meta-package that depends on `Logsmith` with asset filtering (analyzers and build assets only — no compile/runtime). It defaults `LogsmithMode` to `Standalone`, causing the generator to emit all infrastructure as internal types. The embedded Logsmith runtime source files ensure the standalone internal types are always identical to the public types in the runtime library.

## Generated code

For each `[LogMessage]`-decorated partial method, the generator emits:

- A `public const string CategoryName` field with the resolved category name.
- A level-guard early return (`if (!LogManager.IsEnabled(level, category)) return`) with per-category filtering.
- Stack-allocated UTF8 buffer and `Utf8LogWriter` construction.
- Alternating literal UTF8 writes and typed `WriteFormatted` calls for each template segment.
- A `LogEntry` construction with compile-time constants for category, event ID, and source location.
- Dispatch to `LogManager.Dispatch` with both the text span and a static property-writing delegate for structured sinks.
- `[Conditional("DEBUG")]` when the method's level falls at or below the configured threshold.

## Parameter classification

The generator classifies each method parameter by inspecting its type and attributes:

| Classification | Detection | Handling |
|---|---|---|
| Sink | Type is `ILogSink` | Used as dispatch target instead of LogManager |
| AbstractionLogger | Type is `ILogsmithLogger` (abstraction mode) | Used as dispatch target instead of LogsmithOutput |
| Exception | Type is or derives from `Exception` | Attached to `LogEntry.Exception`, excluded from message |
| CallerFile | Has `[CallerFilePath]` | Attached to `LogEntry.CallerFile` |
| CallerLine | Has `[CallerLineNumber]` | Attached to `LogEntry.CallerLine` |
| CallerMember | Has `[CallerMemberName]` | Attached to `LogEntry.CallerMember` |
| Message | Everything else | Matched to template placeholders, formatted to output |

Classification is by attribute and type, not by parameter position. Parameters of any classification can appear in any order.

## Performance characteristics

- **Hot path (logging enabled):** One volatile read (config), one enum comparison (level), stack-allocated buffer, direct UTF8 writes, no heap allocation for value-type parameters.
- **Hot path (logging disabled):** One volatile read, one enum comparison, return. No buffer allocation, no argument formatting.
- **Hot path (with scopes):** One additional null check on `LogScope.Current`. When scopes are active, a 512-byte stack buffer copy for enrichment. When inactive, zero overhead.
- **Hot path (with sampling):** One `Interlocked.Increment` + modulo check. When rate limiting is active, one additional `Volatile.Read` + `Interlocked` pair per call.
- **Conditional-stripped methods:** Zero cost. Call site does not exist in the compiled IL. Arguments are not evaluated.
- **Config swap:** Single volatile write. No lock contention. Subsequent reads on any thread see the new config.
