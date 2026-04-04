# Logsmith v2 — ILogger Rework Design Summary

## Overview

Logsmith v2 replaces the `[LogMessage]` attribute + partial method pattern with an ergonomic `ILogger` API that uses C# interpolated strings directly. The developer writes `logger.Debug($"Draw call {drawCallId} completed in {elapsedMs}ms")` and gets zero-ceremony structured logging with compile-time optimizations via source generator interceptors.

This document captures all design decisions, validated prototypes, open questions, and deliberate exclusions from the brainstorming sessions.

---

## Motivation

The current `[LogMessage]` pattern requires every log statement to be written three times — the template string, the method signature, and the call site:

```csharp
// Current: 7 lines, separate file, duplicated names
[LogCategory("Renderer")]
public static partial class RenderLog
{
    [LogMessage(LogLevel.Debug, "Draw call {drawCallId} completed in {elapsedMs}ms")]
    public static partial void DrawCallCompleted(int drawCallId, double elapsedMs);
}

// Somewhere else:
RenderLog.DrawCallCompleted(id, elapsed);
```

The new API collapses this to a single line at the point of use:

```csharp
logger.Debug($"Draw call {drawCallId} completed in {elapsedMs}ms");
```

The template, the values, and the call site are the same line. The `InterpolatedStringHandler` decomposes the `$"..."` into parts at the call site, the handler writes UTF-8 and structured JSON simultaneously, and optional interceptors add compile-time metadata.

---

## Competitive Landscape

### ZLogger (Cysharp) — Closest Competitor

ZLogger v2 already implements many of the same techniques:
- `InterpolatedStringHandler` for `logger.ZLogDebug($"...")`
- `CallerArgumentExpression` for structured property names
- Typed JSON output (via `MagicalBox` for handler path, direct writes for generator path)
- UTF-8 direct write via `IBufferWriter<byte>`
- Source generator (`[ZLoggerMessage]`) for maximum performance
- Short-circuit via handler `out bool isEnabled`

### What Logsmith v2 Has That ZLogger Does Not

| Feature | ZLogger | Logsmith v2 |
|---------|---------|-------------|
| **Sampling / rate-limiting** | No | First-class: `logger.Sampled(100).Debug(...)` |
| **MEL dependency** | Required (built on top of MEL) | Independent, MEL bridge optional |
| **Standalone mode** | No — ships as NuGet dependency | All types embedded as `internal`, zero surface |
| **Abstraction mode** | No | Library authors expose interfaces without leaking Logsmith |
| **Conditional compilation stripping** | No | `[Conditional]` removes entire call site in Release |
| **Tags / event classification** | No | `logger.Tagged("SQL").Debug(...)` |
| **Timed operations** | No | `logger.TimeOperation($"...")` with correlation IDs |
| **Mutable hierarchical paths** | No | `CreateChild()` + mutable `PathSegment` |
| **Conditional logging** | No | `logger.When(condition).Debug(...)` |
| **Fluent chaining** | No | Full chain with generator-optimized carrier pattern |

### Other Frameworks

- **Serilog**: Rich ecosystem but runtime template parsing, boxing, allocations
- **MEL + [LoggerMessage]**: Same ceremony problem as current Logsmith `[LogMessage]`
- **NLog**: No structured logging at the handler level, runtime-heavy
- **Zap (Go)**: Has `DPanic` and `Check()` patterns — adapted for Logsmith as `DPanic` level and `When()` conditional

---

## Architecture

### Core Types

```
ILogger                          Primary interface, default implementations for all methods
ILogger<T>                       Generic variant, category from type name
LoggerContext                    Internal state: category, path, level, sinks, config
LogCarrier_N (generated)         Per-chain carrier implementing ILogger, thread-static pooled
NullLogger                       Singleton, IsEnabled always false, all methods no-op
DispatchInfo                     Ref struct carrying all dispatch parameters to sinks
LogDebugHandler (etc.)           InterpolatedStringHandler ref structs per log level
LogHandlerCore                   Shared handler implementation (UTF-8 + JSON dual write)
TimingOperation                  Timed operation handle: Complete/Fail/TimeStep + ILogger + IDisposable
```

### The Three Logging Tiers

| Tier | API | When Disabled | Approx. Cost (disabled) | Use Case |
|------|-----|---------------|------------------------|----------|
| **Direct** | `logger.Debug($"...")` | Handler short-circuit | ~3-5ns (IsEnabled + handler ctor + return) | Normal application code |
| **Chained** | `logger.When(x).Sampled(N).Debug($"...")` | NullLogger propagation + empty handler | ~8-15ns (first-call check + NullLogger through chain + empty handler ctor) | Conditional/sampled logging |
| **Static** | `Log.Trace(logger, $"...")` | `[Conditional]` strips entire call site | 0ns (call site deleted from IL) | Hot loops, tight game loops |

All three tiers flow through `LoggerContext.Dispatch()`.

**Note on `[Conditional]` and interfaces**: `[Conditional]` cannot be applied to interface methods — only to methods on classes and structs. This is why the Static tier uses a separate `Log` class rather than methods on `ILogger`.

---

## Design Decisions

### 1. Interface-Only Design — No Sealed Logger Class

**Decision**: `ILogger` is the only user-facing type. It has default implementations for all methods. No `sealed class Logger`.

**Rationale**: Default interface methods eliminate the need for a concrete class hierarchy. The `LoggerContext` holds all state. Wrapper types (carrier, NullLogger) implement `ILogger` to flow through chains.

```csharp
public interface ILogger
{
    LoggerContext Context { get; }

    bool IsEnabled(LogLevel level) => Context.IsEnabled(level);

    // --- Terminal methods: handler overloads (compiler picks for $"...") ---
    // Each level has: handler, handler+exception, string, string+exception

    void Trace([InterpolatedStringHandlerArgument("")] ref LogTraceHandler handler);
    void Trace(Exception? ex, [InterpolatedStringHandlerArgument("", "ex")] ref LogTraceHandler handler);
    void Trace(string message);
    void Trace(Exception? ex, string message);

    void Debug([InterpolatedStringHandlerArgument("")] ref LogDebugHandler handler);
    void Debug(Exception? ex, [InterpolatedStringHandlerArgument("", "ex")] ref LogDebugHandler handler);
    void Debug(string message);
    void Debug(Exception? ex, string message);

    void Information([InterpolatedStringHandlerArgument("")] ref LogInformationHandler handler);
    void Information(Exception? ex, [InterpolatedStringHandlerArgument("", "ex")] ref LogInformationHandler handler);
    void Information(string message);
    void Information(Exception? ex, string message);

    void Warning([InterpolatedStringHandlerArgument("")] ref LogWarningHandler handler);
    void Warning(Exception? ex, [InterpolatedStringHandlerArgument("", "ex")] ref LogWarningHandler handler);
    void Warning(string message);
    void Warning(Exception? ex, string message);

    void Error([InterpolatedStringHandlerArgument("")] ref LogErrorHandler handler);
    void Error(Exception? ex, [InterpolatedStringHandlerArgument("", "ex")] ref LogErrorHandler handler);
    void Error(string message);
    void Error(Exception? ex, string message);

    void Critical([InterpolatedStringHandlerArgument("")] ref LogCriticalHandler handler);
    void Critical(Exception? ex, [InterpolatedStringHandlerArgument("", "ex")] ref LogCriticalHandler handler);
    void Critical(string message);
    void Critical(Exception? ex, string message);

    // --- Chain methods (all return ILogger for fluent chaining) ---

    ILogger When(bool condition) => condition ? this : NullLogger.Instance;
    ILogger Sampled(int rate);      // emit 1-in-N calls
    ILogger RateLimited(int maxPerSecond); // max N per second (sliding window)
    ILogger Tagged(string tag);
    ILogger Tagged(params ReadOnlySpan<string> tags);

    // --- Scoping & hierarchy ---

    ILogger Scoped([InterpolatedStringHandlerArgument("")] ref LogScopeHandler handler);
    ILogger CreateChild(string? segment = null);
    string? PathSegment { get; set; }

    // --- Timed operations ---

    TimingOperation TimeOperation([InterpolatedStringHandlerArgument("")] ref LogTimingHandler handler);
}

public interface ILogger<T> : ILogger
{
    // Category automatically set to typeof(T).Name by factory
}
```

**Handler constructor overloads**: Each handler type has two constructors — one for `ILogger` (direct calls) and one for `ILogger` + `Exception?` (exception overloads). The `[InterpolatedStringHandlerArgument]` attribute tells the compiler which parameters to pass:

```csharp
// Constructor for logger.Debug($"...")
public LogDebugHandler(int literalLength, int formattedCount,
    ILogger logger, out bool isEnabled) { ... }

// Constructor for logger.Debug(ex, $"...")
public LogDebugHandler(int literalLength, int formattedCount,
    ILogger logger, Exception? ex, out bool isEnabled) { ... }
```

### 2. LoggerContext as the Central State Holder

**Decision**: All state lives in `LoggerContext`. Every `ILogger` implementation wraps a context.

```csharp
public class LoggerContext
{
    internal string Category;
    internal PathNode PathNode;
    internal LogLevel MinimumLevel;
    internal LoggerContext? Parent;
    internal SinkSet Sinks;
    internal Action<Exception>? ErrorHandler;
    // ... monitors, category overrides, etc.
    
    internal bool IsEnabled(LogLevel level, string? category = null) { ... }

    /// <summary>
    /// Single dispatch entrypoint for all logging paths.
    /// </summary>
    internal void Dispatch(in DispatchInfo info) { ... }

    /// <summary>
    /// Helper for string overloads — UTF-8 encodes the string, builds DispatchInfo, calls Dispatch.
    /// </summary>
    internal void DispatchString(LogLevel level, string message, Exception? ex = null) { ... }
}

/// <summary>
/// Ref struct carrying all dispatch parameters. Avoids parameter proliferation.
/// </summary>
internal ref struct DispatchInfo
{
    public LogLevel Level;
    public ReadOnlySpan<byte> Text;
    public ReadOnlySpan<byte> Json;
    public Exception? Exception;
    public string? Tag;
    public int EventId;
}
```

**Origin**: Created by `LogManager.GetLogger()` or `LogManager.GetLogger<T>()`. Child contexts are created by `CreateChild()` with a parent link.

**Configuration**: The static `LogManager` holds a root configuration. `LogManager.Initialize(cfg => ...)` sets up sinks, levels, monitors. Child contexts inherit from parent, can override.

### 3. InterpolatedStringHandler with Dual-Buffer (Text + JSON)

**Decision**: Each handler writes BOTH UTF-8 text and structured JSON simultaneously during `AppendFormatted` calls. By the time the terminal method executes, both buffers are ready.

**Validated in**: `prototype/LoggerPrototype/`

```csharp
[InterpolatedStringHandler]
public ref struct LogDebugHandler
{
    private ArrayBufferWriter<byte> _textBuffer;    // UTF-8 text message
    private Utf8JsonWriter _jsonWriter;              // structured properties
    private ArrayBufferWriter<byte> _jsonBuffer;
    private bool _enabled;

    public LogDebugHandler(int literalLength, int formattedCount,
        ILogger logger, out bool isEnabled)
    {
        _enabled = isEnabled = logger.IsEnabled(LogLevel.Debug);
        if (!_enabled) return;
        // Rent buffers from thread-static pool
    }

    public void AppendLiteral(string s)
    {
        if (!_enabled) return;
        Encoding.UTF8.GetBytes(s.AsSpan(), _textBuffer);
        // Literals are not structured properties — skip JSON
    }

    public void AppendFormatted<T>(T value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (!_enabled) return;
        // Text path — UTF-8 encode the value
        WriteTextValue(value, format: null);
        // Structured path — typed JSON property
        WriteJsonProperty(_jsonWriter, name ?? "unknown", value);
    }
}
```

### 4. CallerArgumentExpression for Structured Property Names

**Decision**: Property names come from `[CallerArgumentExpression]` on `AppendFormatted`, not from a template DSL.

**Validated in**: `prototype/LoggerPrototype/` and `prototype/WhenInterceptorTest/`

```csharp
logger.Debug($"User {userId} processing {itemCount} items");
// structured: {"userId":"user-123","itemCount":5}

logger.Info($"Order has {order.Items.Count} items totaling {order.Total:F2}");
// structured: {"order.Items.Count":3,"order.Total":99.95}
```

Simple variables produce clean names. Complex expressions produce verbose but functional names.

### 5. JIT-Specialized Zero-Boxing JSON Writes

**Decision**: Use `typeof(T) == typeof(int)` + `Unsafe.As<T, int>(ref value)` pattern for typed JSON property writes. The JIT eliminates dead branches per callsite.

**Validated in**: `prototype/LoggerPrototype/`

```csharp
private static void WriteJsonProperty<T>(Utf8JsonWriter writer, string name, T value)
{
    if (typeof(T) == typeof(int))
        writer.WriteNumber(name, Unsafe.As<T, int>(ref value));
    else if (typeof(T) == typeof(double))
        writer.WriteNumber(name, Unsafe.As<T, double>(ref value));
    else if (typeof(T) == typeof(bool))
        writer.WriteBoolean(name, Unsafe.As<T, bool>(ref value));
    else if (typeof(T) == typeof(string))
        writer.WriteString(name, Unsafe.As<T, string>(ref value));
    // ... DateTime, Guid, decimal, etc.
    else
        writer.WriteString(name, value?.ToString());
}
```

For `T = int`, the JIT compiles this to just `writer.WriteNumber(name, value)`. All other branches are eliminated.

### 6. String Overloads Work — No Throwing

**Decision**: Plain strings and pre-built interpolated strings go through the string overload. They work correctly, just without structured property decomposition.

```csharp
logger.Debug($"Interpolated {value}");     // handler overload — structured
logger.Debug("Plain string");              // string overload — works, no structured props
string msg = $"Pre-built {value}";
logger.Debug(msg);                         // string overload — values already baked in
```

An analyzer (LSMITH011) warns on the pre-built case since structured properties are lost.

### 7. All Levels Accept Exception as First Parameter

**Decision**: Every log level has overloads with and without `Exception`:

```csharp
logger.Debug($"message");
logger.Debug(ex, $"message with exception context");
logger.Information($"message");
logger.Information(ex, $"message with exception context");
// ... same for Warning, Error, Critical, Trace
```

### 8. Chain Design — Quarry-Inspired Carrier Pattern

**Decision**: Fluent chains use a generator-optimized carrier pattern inspired by Quarry. The generator analyzes the full chain at compile time and generates interceptors for each step.

#### Chain Execution Model

```csharp
// User writes:
logger.When(cond).Sampled(100).Tagged("SQL").Debug($"query {q}");
```

The generator sees the full chain and emits interceptors:

1. **First call** (`.When(cond)`): Creates carrier, does all possible early-out checks (condition, level, sampling). Returns carrier or NullLogger.
2. **Intermediate calls** (`.Sampled(100)`, `.Tagged("SQL")`): Constants baked into first/terminal interceptors. Runtime variables stuffed into carrier fields.
3. **Terminal call** (`.Debug(ref handler)`): Dispatches with all accumulated state.

```csharp
// Generated carrier — one per unique chain shape, thread-static pooled
// Only needs to implement ILogger.Context; all other ILogger methods use default implementations
internal class LogCarrier_0 : ILogger
{
    internal LoggerContext Context;
    internal bool _inUse;       // re-entrancy guard
    internal string? Tag_01;
    // ... fields for runtime chain values
    
    LoggerContext ILogger.Context => Context;
    // All other ILogger methods (When, Sampled, Tagged, Debug, etc.)
    // use default interface implementations — no explicit impl needed
}

[ThreadStatic]
private static LogCarrier_0? t_carrier;

// First call interceptor — creates carrier, early-out checks, re-entrancy guard
[InterceptsLocation(...)]
static ILogger __When_42(this ILogger logger, bool condition)
{
    if (!condition) return NullLogger.Instance;
    if (!logger.Context.IsEnabled(LogLevel.Debug)) return NullLogger.Instance;
    // Sampling — rate 100 baked from .Sampled(100) via static analysis
    if (Interlocked.Increment(ref __counter_42) % 100 != 0) return NullLogger.Instance;
    
    var carrier = t_carrier ??= new LogCarrier_0();
    if (carrier._inUse)
        carrier = new LogCarrier_0(); // rare re-entrant path (sink callback logging)
    carrier._inUse = true;
    carrier.Context = logger.Context;
    return carrier;
}
private static int __counter_42;

// Intermediate — runtime variable stuffed into carrier
[InterceptsLocation(...)]
static ILogger __Tagged_42(this ILogger logger, string tag)
{
    if (logger is LogCarrier_0 c) c.Tag_01 = tag;
    return logger;
}

// Intermediate — constant, already baked into first interceptor
[InterceptsLocation(...)]
static ILogger __Sampled_42(this ILogger logger, int rate)
{
    return logger; // no-op, already handled
}

// Terminal — dispatches with all state, releases carrier
[InterceptsLocation(...)]
static void __Debug_42(this ILogger logger, ref LogDebugHandler handler)
{
    if (!handler.IsEnabled) return;
    if (logger is LogCarrier_0 c)
    {
        c.Context.Dispatch(new DispatchInfo
        {
            Level = LogLevel.Debug,
            Text = handler.GetTextWritten(),
            Json = handler.GetJsonWritten(),
            Tag = c.Tag_01,
            EventId = 0x7A3F2B01  // compile-time FNV-1a hash of template
        });
        c._inUse = false; // release carrier for reuse
    }
}
```

**Re-entrancy**: If a sink callback triggers another log call while a carrier is in use, the `_inUse` guard detects it and allocates a fresh carrier. This is rare (only happens with logging-from-sinks) and the allocation is acceptable. Normal logging path uses the pooled thread-static carrier with zero allocation.

#### Chain Rules (Enforced by Analyzer)

- **`When()` can appear at any position in the chain.** The generator distributes checks to each interceptor based on argument availability. No position restriction.
- **LSMITH013**: Chains must be continuous single expressions. Storing intermediate results in variables is an error.

```csharp
// All valid — When() in any position
logger.When(cond).Sampled(100).Tagged("SQL").Debug($"query {q}");
logger.Sampled(100).When(cond).Debug($"...");
logger.Tagged("SQL").When(cond).Debug($"...");

// LSMITH013 — chain broken by variable
var sampled = logger.Sampled(100);
sampled.Debug($"...");  // error — generator can't see full chain
```

#### When() Without Chain — Default Interface Method

`When()` is a default interface method, no interceptor needed for the simple case:

```csharp
ILogger When(bool condition) => condition ? this : NullLogger.Instance;

// Usage:
logger.When(retryCount > 3).Warning($"Retry {retryCount}");
```

NullLogger propagation: handler constructor receives NullLogger, `IsEnabled` returns false, handler does zero work.

### 9. Compile-Time Stripping via [Conditional]

**Decision**: `[Conditional]` on the ORIGINAL method (not the interceptor) removes the entire call site including handler construction.

**Validated in**: `prototype/ConditionalTest/` and `prototype/InterceptorConditionalTest/`

**Critical finding**: `[Conditional]` on an interceptor method does NOT remove the call. It must be on the original method.

```csharp
// Static methods for hot-loop stripping
public static class Log
{
    [Conditional("LOGSMITH_TRACE")]
    public static void Trace(ILogger logger, ref LogTraceHandler handler)
    {
        if (!handler.IsEnabled) return;
        logger.Context.Dispatch(LogLevel.Trace, ...);
    }

    [Conditional("LOGSMITH_DEBUG")]
    public static void Debug(ILogger logger, ref LogDebugHandler handler)
    {
        if (!handler.IsEnabled) return;
        logger.Context.Dispatch(LogLevel.Debug, ...);
    }

    // Information and above — always present, no [Conditional]
    public static void Information(ILogger logger, ref LogInformationHandler handler) { ... }
}

// Usage — stripped entirely in Release when LOGSMITH_TRACE not defined:
Log.Trace(logger, $"inner loop iteration {i}");
```

MSBuild control:
```xml
<!-- Debug build: all levels -->
<DefineConstants>$(DefineConstants);LOGSMITH_TRACE;LOGSMITH_DEBUG</DefineConstants>

<!-- Release build: omit LOGSMITH_TRACE/LOGSMITH_DEBUG → calls stripped to zero -->
```

### 10. Two-Stage Source Generator

**Decision**: Follow the Quarry pattern with two generator stages.

**Stage 1** (`RegisterSourceOutput` — design-time, IDE sees types):
- Emit `ILogger`, `ILogger<T>`, handler ref structs, `LogLevel`, `LoggerContext`, `NullLogger`, etc.
- These are the types the user codes against

**Stage 2** (`RegisterImplementationSourceOutput` — build-time):
- Scan for all `logger.Debug($"...")`, chain calls, etc.
- Use `SemanticModel.GetInterceptableLocation()` for interceptor targeting
- Emit interceptors for each call site / chain
- Emit per-call-site sampling counters, event IDs, carrier types

**Standalone mode**: No Logsmith package reference. Stage 1 emits all types. Stage 2 (`RegisterImplementationSourceOutput`) should see Stage 1 outputs in the same generator's compilation pipeline. **Verify during implementation**: if Stage 2 cannot resolve Stage 1 types, apply the supplemental compilation trick (`Compilation.AddSyntaxTrees()`) from Quarry as a fallback.

**Shared mode**: Logsmith package provides types via DLL reference. Stage 2 sees them directly in the compilation. No supplemental compilation needed.

### 11. Interceptor API — .NET 10 Current State

**Validated in**: `prototype/InterceptorConditionalTest/` and `prototype/WhenInterceptorTest/`

- The generator must emit `InterceptsLocationAttribute` as a `file class` in `System.Runtime.CompilerServices`. It is NOT in the BCL.
- Constructor signature: `(int version, string data)` — the old `(string path, int line, int column)` is dead (CS9270).
- Values come from `SemanticModel.GetInterceptableLocation(invocation, ct)` returning `InterceptableLocation` with `.Version` and `.Data`.
- MSBuild property: `<InterceptorsNamespaces>` (or legacy `<InterceptorsPreviewNamespaces>`).
- API requires `#pragma warning disable RSEXPERIMENTAL002`.
- Generator must use Roslyn 4.12.0+ for the `GetInterceptableLocation` API.

### 12. Ref Structs Implement Interfaces (.NET 10)

**Validated in**: `prototype/RefStructTest/`

- `ref struct : IDisposable` works with `using` statements
- `ref struct` can implement multiple interfaces
- `allows ref struct` generic constraint enables ref struct as type argument
- Zero heap allocation for ref struct disposables

### 13. Mutable Hierarchical Paths (from NexNet)

**Decision**: Adapt NexNet's `PathNode` pattern for hierarchical logging identity. Paths are separate from categories — category is for filtering, path is for tracing.

```csharp
// Server starts
ILogger serverLog = LogManager.GetLogger("Server");

// Client connects — ID unknown yet
ILogger clientLog = serverLog.CreateChild();
clientLog.Debug($"Client connected from {endpoint}");
// Output: [DBG Server] Client connected from 10.0.0.5

// Server assigns connection ID — update the segment
clientLog.PathSegment = $"Client-{connectionId}";
clientLog.Debug($"Handshake complete");
// Output: [DBG Server|Client-42] Handshake complete

// Sub-stream opens
ILogger streamLog = clientLog.CreateChild($"Stream-{streamId}");
streamLog.Debug($"Data received: {bytes} bytes");
// Output: [DBG Server|Client-42|Stream-7] Data received: 1024 bytes

// Connection ID corrected — ALL child loggers update automatically
clientLog.PathSegment = $"Client-42-R";
streamLog.Debug($"Still going");
// Output: [DBG Server|Client-42-R|Stream-7] Still going
```

Version-based caching: only rebuild formatted path when `CalculateVersionSum()` changes. Zero-alloc path building via ref struct enumerator writing directly to UTF-8 buffer.

**Thread safety**: `PathSegment` setter uses `Volatile.Write` for the string reference and `Interlocked.Increment` for the version counter. `CalculateVersionSum()` uses `Volatile.Read`. This ensures visibility on weakly-ordered architectures (ARM64). On x86, these compile to regular reads/writes (no overhead). Guard behind `#if` ARM compiler flag if needed.

```csharp
public string? Segment
{
    get => Volatile.Read(ref _segment);
    set
    {
        Volatile.Write(ref _segment, value);
        Interlocked.Increment(ref _version);
    }
}
```

### 14. Scoped Logging — Struct, No AsyncLocal

**Decision**: Scopes are explicit struct wrappers, not ambient `AsyncLocal` state. The scope IS a logger that chains to its parent context. **Scopes only add a path segment** — they do not persist structured properties into child log calls.

```csharp
using var reqScope = logger.Scoped($"Request-{requestId}");
// reqScope wraps a new LoggerContext linked to logger's context
// The interpolated string becomes the path segment: "Request-abc"

reqScope.Debug($"Processing {itemId}...");
// Output: [DBG MyApp|Request-abc] Processing item-1...
// structured: {"itemId":"item-1","message":"Processing item-1..."}
// Note: requestId is NOT automatically in the structured output.
// It is in the PATH: MyApp|Request-abc

// Nested:
using var dbScope = reqScope.Scoped($"DB-{queryName}");
dbScope.Debug($"Executing");
// Output: [DBG MyApp|Request-abc|DB-Users] Executing
```

**Why no persistent scope properties**: Persistent properties would require each `Dispatch()` call to walk the context chain and merge parent properties into the JSON output. This adds overhead to every log call, even those with no scope. The path segment already carries the identity. If users need specific properties, they include them in the interpolated string at each call site — this is explicit and zero-overhead when not used.

`Scoped()` returns a regular struct (not ref struct) so it can cross `await` boundaries. The struct holds a `LoggerContext` reference — zero additional heap allocation beyond the context.

### 15. Tags — Orthogonal Event Classification

**Decision**: Tags are separate from category and path. They classify individual events.

```csharp
logger.Tagged("SQL").Debug($"Query {query} returned {rows} rows");
logger.Tagged("HTTP").Information($"GET {path} -> {status}");
logger.Tagged("AUTH", "SECURITY").Warning($"Failed login for {user}");

// Filtering in config:
cfg.SetTagLevel("SQL", LogLevel.Warning);
cfg.SetTagLevel("SECURITY", LogLevel.Trace);
```

### 16. Timed Operations with Correlation IDs

**Decision**: First-class timing with start/complete/fail lifecycle and correlation IDs.

```csharp
using var op = logger.TimeOperation($"Processing-{orderId}");
// Logs: [INF] > Processing-ORD-123 (op:7a3f)
// structured: {"orderId":"ORD-123","operationId":"7a3f","event":"started"}

try
{
    await DoWork();
    op.Complete();
    // Logs: [INF] < Processing-ORD-123 completed in 47.3ms (op:7a3f)
    // structured: {"orderId":"ORD-123","operationId":"7a3f","event":"completed","elapsedMs":47.3}
}
catch (Exception ex)
{
    op.Fail(ex);
    // Logs: [ERR] x Processing-ORD-123 failed after 12.1ms (op:7a3f)
}
// If neither Complete nor Fail called → Dispose logs "abandoned" at Warning

// Sub-operations:
using var sub = op.TimeStep($"Item-{item.Id}");
// Logs: [DBG] > Item-X (op:7a3f/1)
```

Operation ID is a lightweight `Interlocked.Increment` counter (4 hex chars). No GUID overhead.

`IDisposable` cannot detect if an exception was thrown — this is a .NET limitation. The `Complete()`/`Fail(ex)` pattern handles this explicitly.

### 17. DI Integration

**Decision**: Both static factory and DI injection.

```csharp
// Static factory — console apps, game loops, early startup
ILogger logger = LogManager.GetLogger("Renderer");
ILogger<RenderSystem> typed = LogManager.GetLogger<RenderSystem>();

// DI — ASP.NET, hosted services
services.AddLogsmith(cfg =>
{
    cfg.MinimumLevel = LogLevel.Debug;
    cfg.AddConsoleSink(colored: true);
});

public class OrderService(ILogger<OrderService> logger)
{
    public async Task Process(Order order)
    {
        using var scope = logger.Scoped($"Order-{order.Id}");
        scope.Information($"Processing {order.ItemCount} items");

        // CreateChild for sub-components
        var validator = new OrderValidator(logger.CreateChild("Validator"));
    }
}
```

### 18. Zero-Alloc Path Building

**Decision**: Replace `List<string>` with ref struct enumerator writing directly to UTF-8 buffer.

```csharp
public ref struct PathEnumerator
{
    // Walk PathNode chain root-to-leaf
    // Write segments directly to Span<byte> as UTF-8
    // Pipe-delimited: Server|Client-42|Stream-7
    public int WriteUtf8Path(Span<byte> destination) { ... }
}
```

Version-based caching: cache UTF-8 bytes (not string). Only rebuild when `CalculateVersionSum()` changes.

---

## Concepts Consolidated

During brainstorming, we identified overlap between Paths, Tags, Categories, and Scopes. Here's the final model:

| Concept | Lifetime | Mutability | Purpose | Example |
|---------|----------|------------|---------|---------|
| **Category** | Logger creation | Immutable | Filtering by module | `"Renderer"`, `"Database"` |
| **Path** | Logger instance | Mutable segments | Identity/tracing hierarchy | `Server\|Client-42\|Stream-7` |
| **Tags** | Per-call or per-logger | Per-call immutable | Event classification | `#SQL`, `#AUTH`, `#METRICS` |
| **Scope** | Block duration (`using`) | Segment fixed at creation | Transient path segment (no persistent properties) | `Request-abc`, `DB-Users` |

- **Category** = static filter key. One per logger root. Set at creation.
- **Path** = dynamic identity. Hierarchical via `CreateChild()` + mutable `PathSegment`. For long-lived objects (connections, streams).
- **Tags** = semantic labels. Orthogonal to category/path. For event classification and filtering.
- **Scope** = temporary path segment. Created by `Scoped($"...")`. Popped on `Dispose`. Does NOT persist structured properties — use interpolation at each log call site for properties.

---

## What We Are NOT Building

### Not: `[LogMessage]` Attribute Pattern
Completely replaced by `ILogger` + interpolated strings. The `[LogMessage]` pattern required writing every log statement three times. It is being removed entirely.

### Not: Dedicated Catching/Throwing Methods
Considered `logger.Catching(ex, ...)` and `logger.Throwing(ex, ...)` for semantic exception flow distinction. **Rejected** — tags cover this cleanly:
```csharp
logger.Tagged("caught").Error(ex, $"Handled in {operation}");
logger.Tagged("thrown").Error(ex, $"Propagating from {step}");
```

### Not: Lazy Evaluation / Lambda Parameters
The handler `out bool isEnabled` already short-circuits. If the level is disabled, interpolated string expressions are never evaluated. Lambdas (`() => expensive()`) add syntax noise for minimal gain.

### Not: Flow Tracing (Enter/Exit)
Considered `logger.Enter()` / `logger.Exit()` for automatic method tracing. **Rejected** — too much noise. AOP or a future `[Instrument]` attribute would do this better.

### Not: Hierarchical Markers
Considered Log4j2-style parent-child marker hierarchies. **Rejected** — over-engineered. Flat tags with multi-tag support covers 95% of needs.

### Not: Namespace Nesting (Zap-style)
Considered `zap.Namespace("metrics")` for scoping JSON sub-objects. **Rejected** — adds complexity to structured output for rare benefit.

### Not: AsyncLocal Scoping
Explicitly rejected ambient `AsyncLocal`-based scoping. Scopes are explicit — pass the scoped logger, don't rely on hidden ambient state. This avoids `AsyncLocal` overhead and makes scope flow visible in the code.

### Not: Pre-Computed UTF-8 Literals via Interceptors
Considered having interceptors replace handler literal encoding with compile-time `"text"u8` byte literals. **Rejected** — the handler runs before the interceptor, so the interceptor can't change the handler's behavior. The runtime cost of ASCII→UTF-8 encoding is ~1-5ns per literal, negligible compared to sink I/O.

### Not: InterpolatedStringHandler-Only Approach (No Generator)
A handler-only approach (Approach A) works without a generator but loses: compile-time event IDs, per-call-site sampling counters, conditional compilation stripping, carrier chain optimization, and compile-time diagnostics.

---

## Resolved Decisions (from review)

These were flagged during the engineering review and have been resolved:

| ID | Issue | Resolution |
|----|-------|------------|
| C1 | `LogChain` phantom type | **Removed.** All chains flow through `ILogger` — original logger, NullLogger, or generated carrier. |
| C2 | `Scoped()` property persistence | **Scope only adds path segment.** No persistent structured properties — explicit interpolation at each call site. |
| C3 | Missing methods on ILogger | **Full declaration.** All user-callable methods declared on `ILogger` with default implementations. |
| C4 | Re-entrant logging | **Disallow.** If carrier is in use (sink callback), return NullLogger immediately. Silent drop, documented limitation. |
| C5 | Zero-alloc claims | **Prototype gap noted.** Production will use `ThreadBuffer` pooling. Prototype uses `new ArrayBufferWriter` for simplicity. |
| X1 | When() position rule | **Allow anywhere.** LSMITH012 dropped. Generator distributes checks based on argument availability. |
| X2 | Supplemental compilation | **Corrected.** `RegisterImplementationSourceOutput` should see Stage 1 outputs. Verify during implementation; Quarry pattern is fallback. |
| N1 | Handler naming | **Full names.** `LogInformationHandler`, `LogWarningHandler` — consistent with method names. |
| N2/N3 | Dispatch signatures | **Unified.** Single `Dispatch(in DispatchInfo)` method. `DispatchString` is a helper that builds `DispatchInfo`. |
| M1 | RateLimited(N) | **Include.** `logger.RateLimited(10).Warning(...)` — max N per second, sliding window. Same mechanism as v1 `MaxPerSecond`. |
| M2 | sink.AsLogger() | **Removed from migration.** Direct sink usage is a custom wrapper; design TBD. |
| M3 | Handler Exception constructor | **Added.** Each handler has two constructors: `(int, int, ILogger, out bool)` and `(int, int, ILogger, Exception?, out bool)`. |
| M4 | TimingOperation not ILogger | **Implements ILogger.** Timing handle can also be used for intermediate logging within the operation. |
| M5 | GetJsonWritten() idempotency | **Boolean guard.** `_jsonFinalized` flag prevents double `WriteEndObject()`. |
| P1 | PathSegment ARM64 safety | **Volatile.Write/Read** for segment + `Interlocked.Increment` for version. Behind ARM compiler flag. |
| P2/P3 | Text path allocations | **Fix in prototype.** Use `IUtf8SpanFormattable.TryFormat` for text path. Add `Nullable<T>` and enum branches to JSON writer. |
| P4 | Disabled cost estimates | **Cost table added.** Direct ~3-5ns, Chained ~8-15ns, Static 0ns. |
| P5 | Chain detection false positives | **Requirement noted.** Generator must resolve each chain call through `SemanticModel` to confirm `ILogger` receiver type. |

---

## Outstanding Questions and Decisions

### 1. Handler Type Consolidation
**Question**: Do we need separate handler types per log level (`LogDebugHandler`, `LogErrorHandler`, etc.) or can we use a single `LogHandler` with a level parameter?

**Trade-off**: Separate types allow `[InterpolatedStringHandlerArgument]` to pass the level implicitly (from the method name). A single type needs the level passed explicitly, which means either a generic `Log(LogLevel, ...)` method or a different mechanism.

**Leaning**: Separate types — they're thin wrappers around `LogHandlerCore` (already validated in prototype). The boilerplate is in the generator, not user-facing.

### 2. DPanic Level
**Question**: Zap's `DPanic` throws in Debug builds, logs Error in Release. Do we add this as a log level or a method modifier?

**Option A**: New log level `LogLevel.DPanic` between Error and Critical
**Option B**: Method on ILogger: `logger.DPanic($"invariant broken")` that checks build configuration
**Option C**: Chain modifier: `logger.PanicInDebug().Error($"...")`

**Leaning**: Option B — dedicated method, not a level. It's behavioral, not a severity.

### 3. Scoped() Return Type
**Question**: `Scoped()` returns a struct for async boundary crossing. But the struct needs to implement ILogger AND IDisposable. What's the exact type?

**Option A**: `LogScope : ILogger, IDisposable` struct wrapping `LoggerContext`
**Option B**: Returns a new `ILogger` (class) that auto-disposes
**Option C**: Returns a generated carrier implementing ILogger + IDisposable

**Leaning**: Option A — simple struct. The `LoggerContext` is the only heap allocation (shared, pooled).

### 4. Tag Storage in Carrier
**Question**: Tags can be a single string or multiple strings (`Tagged("AUTH", "SECURITY")`). How are they stored in the carrier?

**Option A**: Single `string?` field — most calls have one tag
**Option B**: `string[]?` field — supports multiple tags
**Option C**: Comma-delimited string — simple but parsing overhead

**Leaning**: Option A for common case, with an overload that takes `params ReadOnlySpan<string>` for multi-tag. Note: `params ReadOnlySpan<string>` requires C# 13 (.NET 9+), which is within the .NET 10 target.

### 5. Event ID Generation
**Question**: Current Logsmith uses FNV-1a hash of `"ClassName.MethodName"`. With inline logging, there's no method name. What do we hash?

**Leaning**: Hash the interpolated string template text (literal parts only, not values). `"Draw call  completed in ms"` → stable event ID. The generator sees the template at compile time.

### 6. Configuration Inheritance
**Question**: How does `LoggerContext` inherit configuration from parent? Does `CreateChild()` share the same `SinkSet` or copy it? Can child loggers override minimum level?

**Leaning**: Children share parent's `SinkSet` by reference. `MinimumLevel` can be overridden per child. Category overrides apply based on the child's category (if different from parent).

### 7. Generator Chain Detection Algorithm
**Question**: The generator needs to walk the syntax tree from the terminal `.Debug()` call backwards through the chain.

**Requirement**: The generator must resolve each chain call through `SemanticModel` to confirm the receiver is `ILogger` (not just match on method names). This prevents false positives on unrelated types that happen to have `.When()` or `.Tagged()` methods.

```
InvocationExpression: .Debug($"...")
  MemberAccess: .Debug          ← SemanticModel confirms ILogger receiver
    InvocationExpression: .Tagged("SQL")
      MemberAccess: .Tagged     ← SemanticModel confirms ILogger receiver
        InvocationExpression: .Sampled(100)
          MemberAccess: .Sampled  ← SemanticModel confirms ILogger receiver
            IdentifierName: logger ← SemanticModel confirms ILogger type
```

Walk the chain collecting method names + arguments. The "chain must be continuous" diagnostic (LSMITH013) fires if any intermediate result is stored in a variable.

---

## Prototype Limitations

The prototypes validate the design concepts but have known gaps that will be addressed in production:

| Limitation | Prototype Behavior | Production Fix |
|------------|--------------------|----------------|
| **Buffer allocation** | `new ArrayBufferWriter<byte>()` per log call (5+ heap allocs when enabled) | `ThreadBuffer` pooling — thread-static `ArrayBufferWriter` reuse, zero alloc |
| **Text path formatting** | `value?.ToString()` allocates a string per formatted value | `IUtf8SpanFormattable.TryFormat` writes directly to UTF-8 buffer, zero alloc for primitive types |
| **Nullable JSON** | `int?` falls through to `ToString()` → boxed, written as string | Add `typeof(T) == typeof(int?)` branches with `Unsafe.As<T, int?>(ref value).Value` |
| **Enum JSON** | Enums fall through to `ToString()` → boxed, written as string | Add enum detection via `typeof(T).IsEnum` with JIT-specialized branches |
| **JSON idempotency** | `GetJsonWritten()` throws if called twice | `_jsonFinalized` boolean guard returns cached span on subsequent calls |
| **Re-entrancy** | Not handled — carrier corruption possible | `_inUse` flag on carrier; re-entrant calls return NullLogger (silent drop) |

---

## Prototype Inventory

All prototypes are in `prototype/` on the `feature/ilogger-rework` branch.

| Directory | What it validates |
|-----------|-------------------|
| `LoggerPrototype/` | Handler dual-buffer, CallerArgumentExpression, typed JSON, short-circuit, all levels, string overload |
| `RefStructTest/` | ref struct : IDisposable, ref struct : multiple interfaces, `allows ref struct` constraint |
| `ConditionalTest/` | `[Conditional]` removes entire call site including handler construction and side effects |
| `InterceptorConditionalTest/` | `[Conditional]` on interceptor does NOT work; must be on original method. New interceptor API (version, data). |
| `WhenInterceptorTest/` | When(false) → NullLogger → handler disabled → interceptor skips. Full chain flow validated with real interceptors. |

---

## Migration Path

### From Current Logsmith v1

1. `[LogMessage]` methods → inline `logger.Debug($"...")` at each call site
2. `[LogCategory]` → `LogManager.GetLogger("category")` or `ILogger<T>` 
3. `LogManager.Initialize()` → stays the same (sinks, config)
4. `LogScope.Push()` → `logger.Scoped($"...")` — **model change**: scoping moves from ambient (static push) to explicit (instance method returning a disposable struct). No more `AsyncLocal`.
5. `RecordingSink` → stays the same (or enhanced `RecordingLogger`)
6. Explicit `ILogSink` first parameter → pass the sink to a custom `ILogger` wrapper (design TBD)
7. `SampleRate` attribute → `.Sampled(N)` chain method
8. `MaxPerSecond` attribute → `.RateLimited(N)` chain method
9. `AlwaysEmit` → no equivalent needed (handler short-circuit is already nearly zero)
10. Standalone/Abstraction modes → same concept, new generator stages

### Breaking Changes

All breaking changes are acceptable. There is no legacy compatibility requirement.
