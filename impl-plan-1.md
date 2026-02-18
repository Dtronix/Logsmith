# Implementation Plan 1: Core Runtime Library (`src/Logsmith/`)

## Coverage

This plan implements the following spec sections (per `impl-partition-index.md`):

| # | Feature |
|---|---------|
| 1.1 | Project setup — `Logsmith.csproj`, `net10.0`, zero dependencies |
| 1.2 | `LogLevel` enum (`Trace..None`, `byte` backing) |
| 1.3 | `LogEntry` readonly struct |
| 1.4 | `LogMessageAttribute` |
| 1.5 | `LogCategoryAttribute` |
| 1.6 | `ILogSink` interface |
| 1.7 | `IStructuredLogSink` interface |
| 1.8 | `WriteProperties<TState>` delegate |
| 1.9 | `ILogStructurable` interface |
| 1.10 | `Utf8LogWriter` ref struct |
| 1.11 | Internal `SinkSet` — pre-separated sink arrays |
| 1.12 | Immutable internal `LogConfig` object |
| 1.13 | `LogConfigBuilder` |
| 1.14 | `LogManager` — Initialize, Reconfigure, volatile config swap |
| 1.15 | `LogManager.IsEnabled(LogLevel)` |
| 1.16 | `LogManager.Dispatch<TState>` |

## Dependency Map

### This plan provides to other plans

- **Plan 2 (Phase 1)** depends on Plan 1 Phase 1: `LogLevel`, `LogMessageAttribute`, `LogCategoryAttribute`, `ILogSink`, `IStructuredLogSink`, `ILogStructurable`, `WriteProperties<TState>`, `LogEntry` — generator performs semantic analysis against these types.
- **Plan 2 (Phase 4)** depends on Plan 1 Phase 2: `Utf8LogWriter` signatures, `SinkSet` shape, `LogManager.Dispatch` signature — generator emits code referencing these.
- **Plan 3 (Phase 1)** depends on Plan 1 Phase 1: `ILogSink`, `IStructuredLogSink`, `LogLevel`, `LogEntry` — sink base classes implement these interfaces.
- **Plan 3 (Phase 2)** depends on Plan 1 Phase 2: `Utf8LogWriter`, `LogEntry`, `SinkSet` internals — concrete sinks use these.
- **Plan 3 (Phase 3)** depends on Plan 1 Phase 3: `LogManager`, `LogConfigBuilder` — NuGet packaging bundles the full runtime assembly.
- **Plan 3 (Phase 4)** depends on Plan 1 Phase 3: Tests exercise `LogManager.Initialize`, `Dispatch`, level filtering.

### This plan requires from other plans

- **Plan 3 (Phase 1-2)** provides concrete sink classes (`ConsoleSink`, `FileSink`, `DebugSink`) that `LogConfigBuilder` convenience methods instantiate. Since all code lives in the same `src/Logsmith/` assembly, this is an intra-project ordering concern. Plan 1 Phase 3 `LogConfigBuilder` references sink types defined by Plan 3 Phase 1-2.

---

## Phase 1: Foundational Types, Attributes, and Interfaces

### Entry Criteria

- .NET 10 SDK installed.
- Solution file `SouceGeneratorLogging.sln` exists (or will be created).

### Description

Create the `Logsmith.csproj` project targeting `net10.0` with zero external dependencies. Define all foundational public types: the `LogLevel` enum, the `LogEntry` readonly struct, both attributes (`LogMessageAttribute`, `LogCategoryAttribute`), and all sink-related interfaces/delegates (`ILogSink`, `IStructuredLogSink`, `WriteProperties<TState>`, `ILogStructurable`).

### Core Concepts

- **`LogLevel`**: Seven-member enum with `byte` backing type. Ordinal values (0-6) are used by the generator for conditional compilation threshold comparison and by `LogManager` for level filtering.
- **`LogEntry`**: Immutable value type carrying all metadata for a single log event. Fields are public readonly. `TimestampTicks` is `DateTime.UtcNow.Ticks` captured at call site. `Category` is a compile-time string literal baked in by the generator. `Exception`, `CallerFile`, and `CallerMember` are nullable.
- **`LogMessageAttribute`**: Drives generator discovery. `Message` defaults to empty string (template-free mode). `EventId` defaults to 0 (auto-hash). `AlwaysEmit` defaults to false.
- **`LogCategoryAttribute`**: Applied to containing class. Generator falls back to class name if absent.
- **`ILogSink`**: Base contract for all sinks. Extends `IDisposable`. `Write` receives the entry by `in` reference and the rendered UTF8 text message.
- **`IStructuredLogSink`**: Extends `ILogSink` with `WriteStructured<TState>` that receives a state value and a `WriteProperties<TState>` delegate for writing JSON properties.
- **`WriteProperties<TState>`**: Delegate with `allows ref struct` constraint on `TState`. Enables the generator to pass stack-only state to structured sinks without allocation.
- **`ILogStructurable`**: User-implementable interface for custom types to write themselves as structured JSON via `Utf8JsonWriter`.

### Signatures

**File: `src/Logsmith/Logsmith.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

**File: `src/Logsmith/LogLevel.cs`**
```csharp
namespace Logsmith;
public enum LogLevel : byte
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical,
    None
}
```

**File: `src/Logsmith/LogEntry.cs`**
```csharp
namespace Logsmith;
public readonly struct LogEntry
{
    public readonly LogLevel Level;
    public readonly int EventId;
    public readonly long TimestampTicks;
    public readonly string Category;
    public readonly Exception? Exception;
    public readonly string? CallerFile;
    public readonly int CallerLine;
    public readonly string? CallerMember;

    public LogEntry(
        LogLevel level,
        int eventId,
        long timestampTicks,
        string category,
        Exception? exception = null,
        string? callerFile = null,
        int callerLine = 0,
        string? callerMember = null);
}
```

**File: `src/Logsmith/Attributes/LogMessageAttribute.cs`**
```csharp
namespace Logsmith;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LogMessageAttribute : Attribute
{
    public LogLevel Level { get; }
    public string Message { get; }
    public int EventId { get; set; }
    public bool AlwaysEmit { get; set; }
    public LogMessageAttribute(LogLevel level, string message = "");
}
```

**File: `src/Logsmith/Attributes/LogCategoryAttribute.cs`**
```csharp
namespace Logsmith;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class LogCategoryAttribute : Attribute
{
    public string Name { get; }
    public LogCategoryAttribute(string name);
}
```

**File: `src/Logsmith/Sinks/ILogSink.cs`**
```csharp
namespace Logsmith;
public interface ILogSink : IDisposable
{
    bool IsEnabled(LogLevel level);
    void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
}
```

**File: `src/Logsmith/Sinks/IStructuredLogSink.cs`**
```csharp
namespace Logsmith;
public interface IStructuredLogSink : ILogSink
{
    void WriteStructured<TState>(
        in LogEntry entry,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct;
}
```

**File: `src/Logsmith/Sinks/WriteProperties.cs`**
```csharp
namespace Logsmith;
public delegate void WriteProperties<TState>(
    System.Text.Json.Utf8JsonWriter writer,
    TState state)
    where TState : allows ref struct;
```

**File: `src/Logsmith/Sinks/ILogStructurable.cs`**
```csharp
namespace Logsmith;
public interface ILogStructurable
{
    void WriteStructured(System.Text.Json.Utf8JsonWriter writer);
}
```

### Phase Deliverables

- `src/Logsmith/Logsmith.csproj` compiles with `dotnet build`.
- All foundational types are defined with correct namespaces, access modifiers, and signatures.
- No external package dependencies.
- Plan 2 Phase 1 and Plan 3 Phase 1 are unblocked.

---

## Phase 2: Utf8LogWriter, SinkSet, and LogConfig

### Entry Criteria

- Phase 1 complete: all foundational types compile.

### Description

Implement the `Utf8LogWriter` ref struct for zero-allocation UTF8 message construction, the internal `SinkSet` class for pre-classified sink arrays, and the immutable internal `LogConfig` object that bundles level thresholds and sinks into a single atomically swappable reference.

### Core Concepts

- **`Utf8LogWriter`**: A `ref struct` wrapping a caller-provided `Span<byte>` buffer. Tracks write position via `BytesWritten`. `Write` copies raw UTF8 literal bytes. `WriteFormatted<T>` constrains `T : IUtf8SpanFormattable` and calls `TryFormat` directly into the remaining buffer span. `WriteString` transcodes a `string?` to UTF8 (writes `"null"u8` for null). `GetWritten` returns the filled slice `buffer[..BytesWritten]`.
- **`SinkSet`**: Sealed internal class holding two arrays: `ILogSink[]` (text-only sinks) and `IStructuredLogSink[]` (structured sinks). Classification rule: when a sink is registered, if it implements `IStructuredLogSink`, it goes into both `StructuredSinks` and `TextSinks` (text fallback). If it only implements `ILogSink`, it goes into `TextSinks` only. Arrays are built once at configuration time and never mutated.
- **`LogConfig`**: Sealed internal class holding `MinimumLevel`, a `Dictionary<string, LogLevel>` for per-category overrides, and a `SinkSet`. Constructed as an immutable snapshot by `LogConfigBuilder.Build()`. Swapped atomically via volatile write in `LogManager`.

### Algorithm: SinkSet Classification

Given a list of `ILogSink` instances:
1. Partition into two lists: those that also implement `IStructuredLogSink` and those that do not.
2. `TextSinks` = all sinks (both plain and structured, preserving registration order).
3. `StructuredSinks` = only those that implement `IStructuredLogSink`, cast to `IStructuredLogSink[]`.
4. Store as arrays (not lists) for iteration performance.

### Signatures

**File: `src/Logsmith/Utf8LogWriter.cs`**
```csharp
namespace Logsmith;
public ref struct Utf8LogWriter
{
    private readonly Span<byte> _buffer;
    private int _position;

    public Utf8LogWriter(Span<byte> buffer);
    public void Write(ReadOnlySpan<byte> utf8Literal);
    public void WriteFormatted<T>(T value) where T : IUtf8SpanFormattable;
    public void WriteString(string? value);
    public ReadOnlySpan<byte> GetWritten();
    public int BytesWritten { get; }
}
```

**File: `src/Logsmith/Internal/SinkSet.cs`**
```csharp
namespace Logsmith.Internal;
internal sealed class SinkSet
{
    internal readonly ILogSink[] TextSinks;
    internal readonly IStructuredLogSink[] StructuredSinks;

    internal SinkSet(ILogSink[] textSinks, IStructuredLogSink[] structuredSinks);

    /// <summary>
    /// Classifies a flat list of sinks into pre-separated arrays.
    /// </summary>
    internal static SinkSet Classify(List<ILogSink> sinks);
}
```

**File: `src/Logsmith/Internal/LogConfig.cs`**
```csharp
namespace Logsmith.Internal;
internal sealed class LogConfig
{
    internal readonly LogLevel MinimumLevel;
    internal readonly Dictionary<string, LogLevel> CategoryOverrides;
    internal readonly SinkSet Sinks;

    internal LogConfig(
        LogLevel minimumLevel,
        Dictionary<string, LogLevel> categoryOverrides,
        SinkSet sinks);
}
```

### Phase Deliverables

- `Utf8LogWriter` ref struct with full write API (literal, formatted, string, get-written).
- `SinkSet` with classification logic separating text and structured sinks.
- Immutable `LogConfig` snapshot object.
- Plan 2 Phase 4 (`impl-plan-2.md`, Phase 4) and Plan 3 Phase 2 (`impl-plan-3.md`, Phase 2) are unblocked.

---

## Phase 3: LogConfigBuilder, LogManager, IsEnabled, and Dispatch

### Entry Criteria

- Phase 2 complete: `Utf8LogWriter`, `SinkSet`, and `LogConfig` compile.
- Plan 3 Phase 1-2 complete (concrete sink types `ConsoleSink`, `FileSink`, `DebugSink` exist in `src/Logsmith/Sinks/` for convenience method instantiation). Alternatively, convenience methods can be stubbed and filled once Plan 3 delivers the sink types, since all code is in the same assembly.

### Description

Implement `LogConfigBuilder` (the mutable builder exposed to users for configuration), and `LogManager` (the static entry point for initialization, reconfiguration, level checking, and dispatch). The critical design constraint is zero-lock, zero-allocation on the hot read/dispatch path.

### Core Concepts

- **`LogConfigBuilder`**: Mutable builder accumulating sink registrations and level overrides. `AddSink` appends to an internal `List<ILogSink>`. `SetMinimumLevel(category, level)` stores per-category overrides in a dictionary. Convenience methods (`AddConsoleSink`, `AddFileSink`, `AddDebugSink`) instantiate the corresponding Plan 3 sink types and call `AddSink`. `ClearSinks` empties the sink list. An internal `Build()` method creates an immutable `LogConfig` snapshot by calling `SinkSet.Classify` on the accumulated sinks.
- **`LogManager.Initialize`**: Creates a `LogConfigBuilder`, invokes the user's `Action<LogConfigBuilder>`, calls `Build()`, and stores the resulting `LogConfig` via volatile write. Throws if already initialized (or use a flag).
- **`LogManager.Reconfigure`**: Same as `Initialize` but allowed after initialization. Creates a fresh builder (not preserving old config), invokes the action, builds, and volatile-swaps the config reference.
- **`LogManager.IsEnabled(LogLevel)`**: Performs a single volatile read of the current `LogConfig`, compares `level` against `MinimumLevel`. Returns `false` if `level < MinimumLevel` or if config is null. This is the hot-path guard emitted by the generator at the top of every log method.
- **`LogManager.Dispatch<TState>`**: Generic method with `allows ref struct` on `TState`. Performs one volatile read of the `SinkSet`. Iterates `TextSinks` array: for each sink, checks `IsEnabled`, then calls `Write(in entry, utf8Message)`. Iterates `StructuredSinks` array: for each sink, checks `IsEnabled`, then calls `WriteStructured<TState>(in entry, state, propertyWriter)`. No allocations, no locks.

### Algorithm: Dispatch Hot Path

```
Dispatch<TState>(in LogEntry entry, ReadOnlySpan<byte> utf8Message, TState state, WriteProperties<TState> propertyWriter):
  config = volatile read _config
  if config is null: return
  sinkSet = config.Sinks
  for each sink in sinkSet.TextSinks:
    if sink.IsEnabled(entry.Level):
      sink.Write(in entry, utf8Message)
  for each sink in sinkSet.StructuredSinks:
    if sink.IsEnabled(entry.Level):
      sink.WriteStructured<TState>(in entry, state, propertyWriter)
```

### Algorithm: IsEnabled with Category Overrides

`IsEnabled(LogLevel level)` uses only the global minimum level (no category parameter). The per-category check is performed by generated code which can call an internal overload or inline the check. The public `IsEnabled` is the fast path for the common case.

```
IsEnabled(LogLevel level):
  config = volatile read _config
  if config is null: return false
  return level >= config.MinimumLevel
```

### Signatures

**File: `src/Logsmith/LogConfigBuilder.cs`**
```csharp
namespace Logsmith;
public sealed class LogConfigBuilder
{
    private readonly List<ILogSink> _sinks;
    private readonly Dictionary<string, LogLevel> _categoryOverrides;

    public LogLevel MinimumLevel { get; set; }

    public LogConfigBuilder();
    public void SetMinimumLevel(string category, LogLevel level);
    public void AddSink(ILogSink sink);
    public void AddConsoleSink(bool colored = true);
    public void AddFileSink(string path);
    public void AddDebugSink();
    public void ClearSinks();

    internal LogConfig Build();
}
```

**File: `src/Logsmith/LogManager.cs`**
```csharp
namespace Logsmith;
public static class LogManager
{
    private static volatile LogConfig? _config;

    public static void Initialize(Action<LogConfigBuilder> configure);
    public static void Reconfigure(Action<LogConfigBuilder> configure);
    public static bool IsEnabled(LogLevel level);

    internal static void Dispatch<TState>(
        in LogEntry entry,
        ReadOnlySpan<byte> utf8Message,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct;
}
```

### Phase Deliverables

- `LogConfigBuilder` with full builder API including convenience sink methods.
- `LogManager` with `Initialize`, `Reconfigure`, `IsEnabled`, and `Dispatch`.
- Volatile config swap with zero locks on the read path.
- All Plan 1 types are complete. Plan 2 Phase 4 and Plan 3 Phase 3-4 (`impl-plan-3.md`, Phases 3-4) are unblocked.
- The full `src/Logsmith/` project compiles as a complete runtime library.
