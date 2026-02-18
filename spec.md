# Logsmith Specification

## Overview

Logsmith is a zero-allocation, source-generator-driven C# logging library. A Roslyn IncrementalGenerator produces all log method bodies at compile time, emitting direct UTF8 output with no reflection, no boxing to `object`, and no runtime template parsing. The system ships as two NuGet packages: `Logsmith` (public runtime types + bundled generator) and `Logsmith.Generator` (standalone analyzer-only), where the generator embeds the runtime source files to guarantee a single source of truth.

## Decisions

### Target & Language

- **Target framework: .NET 10.** The runtime library targets `net10.0`. The generator targets `netstandard2.0` (Roslyn analyzer requirement).
- **No reflection, ever.** The generator knows concrete types at compile time and emits direct calls. No `Type.GetMethod`, no `Activator.CreateInstance`, no `MethodInfo.Invoke`.
- **No boxing to `object`, ever.** Numeric and struct types flow through generic or concrete-typed paths. No parameter is ever widened to `object`.
- **Minimal string comparison.** Use enums for categorical values (log levels, parameter kinds). String matching limited to compile-time template placeholder resolution in the generator.
- **Testing: NUnit only.** Use NUnit's built-in fluent constraint model (`Assert.That`, `Is.EqualTo`, `Has.Count`, `Does.Contain`). **Banned packages: FluentAssertions, Moq.**

### Package Architecture

- **`Logsmith` NuGet bundles the generator transitively.** The package contains `lib/net10.0/Logsmith.dll` and `analyzers/dotnet/cs/Logsmith.Generator.dll`. Referencing `Logsmith` gives consumers both the runtime types and the source generator with no additional `PackageReference`.
- **`Logsmith.Generator` is also available standalone** as `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`. Zero runtime DLLs in consumer output. When referenced alone, the generator emits all infrastructure types as `internal`.
- **The generator embeds Logsmith's source files as `EmbeddedResource`.** The `.cs` files from the `Logsmith` project are embedded in the generator assembly. In standalone mode, the generator reads these resources and emits them with `public` replaced by `internal` at type declaration sites. This guarantees zero divergence between the runtime package types and the standalone-emitted types.
- **Mode detection at generation time:** The generator calls `compilation.GetTypeByMetadataName("Logsmith.LogLevel")` and checks `ContainingAssembly.Name == "Logsmith"`. Present = shared mode (emit method bodies only). Absent = standalone mode (emit infrastructure + method bodies).

### Sink Model

- **Static `LogManager` is the default dispatch target.** Log methods require no sink parameter; generated code calls `LogManager.Dispatch(...)` internally.
- **Optional explicit `ILogSink` as first parameter.** The generator detects if the first parameter's type is `ILogSink` and uses it instead of the global `LogManager`. This supports testing and custom routing without forcing DI on all consumers.
- **Category-based routing lives on `LogManager`, not on per-call sink parameters.** Sinks are registered globally; per-category minimum levels and sink routing are configured via `LogManager.Initialize`/`Reconfigure`.

### Log Categories

- **`[LogCategory("Name")]` on the containing class, or class name as default.** If the attribute is absent, the generator uses the class name as the category string.
- **Category is a compile-time constant** baked into the generated method body as a string literal. It is a field on `LogEntry`.

### Message Templates

- **Both modes supported.** Explicit template string: `[LogMessage(LogLevel.Debug, "Draw call {drawCallId} completed in {elapsedMs}ms")]`. Template-free: `[LogMessage(LogLevel.Debug)]`, where the generator auto-produces `"MethodName drawCallId={drawCallId} elapsedMs={elapsedMs}"` from the method name and parameter names.
- **Template-free mode is always rename-safe.** Renaming parameters in the IDE updates the generated message on next build. Template string mode requires manual sync, enforced by compile-time diagnostics.
- **Placeholder matching is case-insensitive** between `{placeholderName}` and the method parameter name.

### Debug-Only Compilation

- **`[Conditional("DEBUG")]` is auto-applied by the generator** to any log method whose `LogLevel` is at or below a configurable threshold.
- **MSBuild property `<LogsmithConditionalLevel>`** controls the threshold. Default: `Debug`. Values: `Trace`, `Debug`, `Information`, `None`. At `Debug` (default), methods with `LogLevel.Trace` or `LogLevel.Debug` receive `[Conditional("DEBUG")]`.
- **Per-method override via `AlwaysEmit = true`** on the attribute forces the method to always compile regardless of the threshold.

### Caller Information

- **The source generator cannot walk invocation sites.** Caller info uses the compiler's `[CallerFilePath]`, `[CallerLineNumber]`, `[CallerMemberName]` attributes, which are essentially free (interned string literals and int constants).
- **Caller parameters are opt-in per method**, declared by the user on the partial method signature. They are accepted in any order and in any combination (all three, any two, any one, or none).
- **Diagnostic LSMITH005** fires if a caller-attributed parameter name also appears as a placeholder in the message template.

### Nullable Types

- **Nullable value types (`T?` where `T : struct`):** Generator emits `HasValue` guard. Text path writes value or `"null"u8`. Structured path writes value or `Utf8JsonWriter.WriteNull`.
- **Nullable reference types (`string?`, `Exception?`, custom classes):** Generator emits `is not null` guard with same text/structured null handling.

### Custom Type Serialization

- **Text path priority chain (compile-time selected, no runtime type checks):** `IUtf8SpanFormattable` (direct UTF8, zero alloc) > `ISpanFormattable` (stack char buffer, transcode) > `IFormattable` (ToString with format) > `ToString()` (last resort).
- **Structured path:** Types implementing `ILogStructurable` get their `WriteStructured(Utf8JsonWriter)` called. Otherwise the text representation is written as a JSON string value.

### Structured Output

- **UTF8 JSON via `System.Text.Json.Utf8JsonWriter`.** Ships with .NET 10, zero external dependency, zero-allocation writes.

### EventId

- **Auto-generated as a stable hash of `"ClassName.MethodName"`.** Deterministic across builds. User can override via `EventId` property on the attribute.

### Scopes

- **Not supported in v1.** No `BeginScope` equivalent.

### LogManager Configuration

- **`Initialize` for first-time setup. `Reconfigure` for runtime changes.** Both accept `Action<LogConfigBuilder>`.
- **Immutable config object swapped via volatile write.** Hot path reads a single volatile reference. No locks, no `Interlocked` on the read path.
- **Per-category minimum level overrides** supported at the `LogManager` level.

## Concepts & Algorithms

### Parameter Classification

The generator classifies every method parameter by inspecting its type and attributes. Classification is by attribute/type identity, not by position. The six kinds are: `Sink` (type is `ILogSink`), `Exception` (type is or derives from `Exception`), `CallerFile` (has `[CallerFilePath]`), `CallerLine` (has `[CallerLineNumber]`), `CallerMember` (has `[CallerMemberName]`), `MessageParam` (everything else). Only `MessageParam` parameters participate in template placeholder matching.

### Embedded Source Emission

In standalone mode, the generator reads each embedded resource, performs visibility replacement (`public enum ` to `internal enum `, `public interface ` to `internal interface `, `public struct ` to `internal struct `, `public ref struct ` to `internal ref struct `, `public sealed class ` to `internal sealed class `, `public static class ` to `internal static class `, `public class ` to `internal class `), and adds the result to `SourceProductionContext`.

### Dispatch Hot Path

`LogManager.Dispatch` is generic over `TState` with `allows ref struct` constraint. It performs one volatile read of the `SinkSet`, then iterates two pre-separated arrays: `ILogSink[]` for text sinks and `IStructuredLogSink[]` for structured sinks. Sink classification happens once at registration time, never at dispatch time.

### Conditional Compilation Logic

The generator compares the method's `LogLevel` to the `LogsmithConditionalLevel` MSBuild property (read via `AnalyzerConfigOptionsProvider`). If the method's level ordinal is less than or equal to the threshold ordinal, the generator emits `[System.Diagnostics.Conditional("DEBUG")]` on the method. `AlwaysEmit = true` on the attribute bypasses this check.

## Interfaces

### Attributes

```csharp
namespace Logsmith;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LogMessageAttribute : Attribute
{
    public LogLevel Level { get; }
    public string Message { get; }          // empty string = template-free mode
    public int EventId { get; set; }        // 0 = auto-generated hash
    public bool AlwaysEmit { get; set; }    // bypass Conditional threshold
    public LogMessageAttribute(LogLevel level, string message = "");
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class LogCategoryAttribute : Attribute
{
    public string Name { get; }
    public LogCategoryAttribute(string name);
}
```

### Core Types

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
}
```

### Sink Interfaces

```csharp
namespace Logsmith;

public interface ILogSink : IDisposable
{
    bool IsEnabled(LogLevel level);
    void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
}

public interface IStructuredLogSink : ILogSink
{
    void WriteStructured<TState>(
        in LogEntry entry,
        TState state,
        WriteProperties<TState> propertyWriter)
        where TState : allows ref struct;
}

public delegate void WriteProperties<TState>(Utf8JsonWriter writer, TState state)
    where TState : allows ref struct;

public interface ILogStructurable
{
    void WriteStructured(Utf8JsonWriter writer);
}
```

### LogManager

```csharp
namespace Logsmith;

public static class LogManager
{
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

### LogConfigBuilder

```csharp
namespace Logsmith;

public sealed class LogConfigBuilder
{
    public LogLevel MinimumLevel { get; set; }
    public void SetMinimumLevel(string category, LogLevel level);
    public void AddSink(ILogSink sink);
    public void AddConsoleSink(bool colored = true);
    public void AddFileSink(string path);
    public void AddDebugSink();
    public void ClearSinks();
}
```

### V1 Sinks

```csharp
namespace Logsmith.Sinks;

public class ConsoleSink : ILogSink;           // ANSI-colored UTF8 to stdout
public class FileSink : ILogSink, IAsyncDisposable;  // Channel<T> async-buffered, rolling
public class DebugSink : ILogSink;             // System.Diagnostics.Debug output
public class RecordingSink : ILogSink;         // List<CapturedEntry> for test assertions
public class NullSink : ILogSink;              // Discards everything
public abstract class TextLogSink : ILogSink;  // Base for text-only sinks
public abstract class BufferedLogSink : ILogSink;  // Base for async-buffered sinks
```

### Utf8LogWriter

```csharp
namespace Logsmith;

public ref struct Utf8LogWriter
{
    public Utf8LogWriter(Span<byte> buffer);
    public void Write(ReadOnlySpan<byte> utf8Literal);
    public void WriteFormatted<T>(T value) where T : IUtf8SpanFormattable;
    public void WriteString(string? value);
    public ReadOnlySpan<byte> GetWritten();
    public int BytesWritten { get; }
}
```

### Generator Diagnostics

| ID | Severity | Condition |
|----|----------|-----------|
| LSMITH001 | Error | Placeholder `{name}` has no matching parameter |
| LSMITH002 | Warning | Parameter not referenced in message template |
| LSMITH003 | Error | Method must be `static partial` in a `partial class` |
| LSMITH004 | Error | Parameter type has no supported formatting path |
| LSMITH005 | Warning | Caller-attributed parameter name also appears as template placeholder |

### User-Declared Method Shape

```csharp
[LogCategory("Renderer")]
public static partial class RenderLog
{
    // Template mode, all features
    [LogMessage(LogLevel.Debug, "Draw call {drawCallId} completed in {elapsedMs}ms")]
    public static partial void DrawCallCompleted(
        int drawCallId,
        double elapsedMs);

    // Template-free mode
    [LogMessage(LogLevel.Debug)]
    public static partial void FrameTick(int frameId, long triangleCount);

    // Explicit sink + exception + caller info in arbitrary order
    [LogMessage(LogLevel.Error, "Shader failed: {shaderName}")]
    public static partial void ShaderFailed(
        ILogSink sink,
        string shaderName,
        Exception? ex = null,
        [CallerFilePath] string file = "",
        [CallerLineNumber] int line = 0);
}
```

## Project Structure

```
src/Logsmith/                          → net10.0, zero dependencies, public types
src/Logsmith.Generator/                → netstandard2.0, IncrementalGenerator, analyzer-only NuGet
tests/Logsmith.Tests/                  → NUnit, runtime behavior tests
tests/Logsmith.Generator.Tests/        → NUnit, generator compilation tests
```
