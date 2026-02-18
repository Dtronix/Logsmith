# Cross-Plan Interface Contracts

## Project File Structure

```
SouceGeneratorLogging.sln
src/
  Logsmith/
    Logsmith.csproj                          (Plan 1)
    LogLevel.cs                              (Plan 1)
    LogEntry.cs                              (Plan 1)
    Attributes/
      LogMessageAttribute.cs                 (Plan 1)
      LogCategoryAttribute.cs                (Plan 1)
    Sinks/
      ILogSink.cs                            (Plan 1)
      IStructuredLogSink.cs                  (Plan 1)
      ILogStructurable.cs                    (Plan 1)
      WriteProperties.cs                     (Plan 1)
      TextLogSink.cs                         (Plan 3)
      BufferedLogSink.cs                     (Plan 3)
      ConsoleSink.cs                         (Plan 3)
      FileSink.cs                            (Plan 3)
      DebugSink.cs                           (Plan 3)
      RecordingSink.cs                       (Plan 3)
      NullSink.cs                            (Plan 3)
    Utf8LogWriter.cs                         (Plan 1)
    LogManager.cs                            (Plan 1)
    LogConfigBuilder.cs                      (Plan 1)
    Internal/
      SinkSet.cs                             (Plan 1)
      LogConfig.cs                           (Plan 1)
  Logsmith.Generator/
    Logsmith.Generator.csproj                (Plan 2)
    LogsmithGenerator.cs                     (Plan 2)
    Models/
      ParameterKind.cs                       (Plan 2)
      LogMethodInfo.cs                       (Plan 2)
      ParameterInfo.cs                       (Plan 2)
      TemplatePart.cs                        (Plan 2)
    Parsing/
      TemplateParser.cs                      (Plan 2)
      ParameterClassifier.cs                 (Plan 2)
    Emission/
      MethodEmitter.cs                       (Plan 2)
      TextPathEmitter.cs                     (Plan 2)
      StructuredPathEmitter.cs               (Plan 2)
      EmbeddedSourceEmitter.cs               (Plan 2)
    Diagnostics/
      DiagnosticDescriptors.cs               (Plan 2)
tests/
  Logsmith.Tests/
    Logsmith.Tests.csproj                    (Plan 3)
    LogManagerTests.cs                       (Plan 3)
    SinkTests/
      ConsoleSinkTests.cs                    (Plan 3)
      FileSinkTests.cs                       (Plan 3)
      DebugSinkTests.cs                      (Plan 3)
      RecordingSinkTests.cs                  (Plan 3)
      NullSinkTests.cs                       (Plan 3)
  Logsmith.Generator.Tests/
    Logsmith.Generator.Tests.csproj          (Plan 3)
    ParameterClassificationTests.cs          (Plan 3)
    TemplateParsingTests.cs                  (Plan 3)
    DiagnosticTests.cs                       (Plan 3)
    CodeEmissionTests.cs                     (Plan 3)
    StandaloneModeTests.cs                   (Plan 3)
```

## Shared Types Crossing Plan Boundaries

### Plan 1 produces → Plan 2 and Plan 3 consume

#### `Logsmith.LogLevel` (enum)
```csharp
namespace Logsmith;
public enum LogLevel : byte { Trace, Debug, Information, Warning, Error, Critical, None }
```
- **Consumed by Plan 2**: Generator reads LogLevel from attribute to determine conditional compilation threshold, compares ordinals, emits level literals in generated code.
- **Consumed by Plan 3**: Sink implementations check `IsEnabled(LogLevel)`. Tests assert level filtering.

#### `Logsmith.LogEntry` (readonly struct)
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
}
```
- **Consumed by Plan 2**: Generator emits code that constructs `LogEntry` instances, populating fields from classified parameters.
- **Consumed by Plan 3**: Sink `Write` methods receive `in LogEntry`. `RecordingSink` stores entries. Tests inspect entry fields.

#### `Logsmith.LogMessageAttribute` (attribute)
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
- **Consumed by Plan 2**: Generator reads this attribute from syntax/semantic model to extract level, template, eventId, alwaysEmit.

#### `Logsmith.LogCategoryAttribute` (attribute)
```csharp
namespace Logsmith;
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class LogCategoryAttribute : Attribute
{
    public string Name { get; }
    public LogCategoryAttribute(string name);
}
```
- **Consumed by Plan 2**: Generator reads containing class for this attribute; falls back to class name if absent.

#### `Logsmith.ILogSink` (interface)
```csharp
namespace Logsmith;
public interface ILogSink : IDisposable
{
    bool IsEnabled(LogLevel level);
    void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
}
```
- **Consumed by Plan 2**: Generator detects `ILogSink` as first parameter type to enable explicit sink routing.
- **Consumed by Plan 3**: All sink classes implement this. Tests assert against it.

#### `Logsmith.IStructuredLogSink` (interface)
```csharp
namespace Logsmith;
public interface IStructuredLogSink : ILogSink
{
    void WriteStructured<TState>(in LogEntry entry, TState state, WriteProperties<TState> propertyWriter)
        where TState : allows ref struct;
}
```
- **Consumed by Plan 2**: Generator emits dispatch code that calls `WriteStructured` on structured sinks.
- **Consumed by Plan 3**: Structured sink implementations. Tests validate structured output.

#### `Logsmith.WriteProperties<TState>` (delegate)
```csharp
namespace Logsmith;
public delegate void WriteProperties<TState>(Utf8JsonWriter writer, TState state)
    where TState : allows ref struct;
```
- **Consumed by Plan 2**: Generator emits a static method matching this delegate signature for each log method's structured path.

#### `Logsmith.ILogStructurable` (interface)
```csharp
namespace Logsmith;
public interface ILogStructurable
{
    void WriteStructured(Utf8JsonWriter writer);
}
```
- **Consumed by Plan 2**: Generator checks if a parameter type implements `ILogStructurable` to choose structured serialization path.

#### `Logsmith.Utf8LogWriter` (ref struct)
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
- **Consumed by Plan 2**: Generator emits code that instantiates `Utf8LogWriter`, calls `Write`/`WriteFormatted`/`WriteString` for each template segment, then passes `GetWritten()` to dispatch.

#### `Logsmith.LogManager` (static class — dispatch entry point)
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
- **Consumed by Plan 2**: Generator emits calls to `LogManager.IsEnabled()` for early-exit and `LogManager.Dispatch(...)` for the default (non-explicit-sink) path.
- **Consumed by Plan 3**: Tests call `Initialize`/`Reconfigure`. Tests validate dispatch behavior.

#### `Logsmith.LogConfigBuilder` (sealed class)
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
- **Consumed by Plan 3**: Sink convenience methods (`AddConsoleSink`, `AddFileSink`, `AddDebugSink`) create Plan 3 sink instances. Tests exercise all builder methods.

#### Internal `SinkSet` (Plan 1 internal)
```csharp
namespace Logsmith.Internal;
internal sealed class SinkSet
{
    internal ILogSink[] TextSinks;
    internal IStructuredLogSink[] StructuredSinks;
    // Classification: at registration, each ILogSink is checked for IStructuredLogSink.
    // IStructuredLogSink sinks go into both arrays (text fallback + structured path).
    // Plain ILogSink sinks go into TextSinks only.
}
```
- **Consumed by Plan 3**: `LogConfigBuilder.AddSink` builds SinkSet. Tests validate sink classification.

#### Internal `LogConfig` (Plan 1 internal)
```csharp
namespace Logsmith.Internal;
internal sealed class LogConfig
{
    internal LogLevel MinimumLevel;
    internal Dictionary<string, LogLevel> CategoryOverrides;
    internal SinkSet Sinks;
}
```
- Not directly consumed cross-plan, but shape informs Plan 3 test expectations.

### Plan 2 produces → Plan 3 consumes

#### Generator Assembly (`Logsmith.Generator.dll`)
- **Consumed by Plan 3, Phase 3**: NuGet packaging bundles generator DLL into `analyzers/dotnet/cs/`.
- **Consumed by Plan 3, Phase 5**: Generator tests invoke the generator via Roslyn test infrastructure.

#### Embedded Source Resources
- Plan 2's build embeds Plan 1's `.cs` files as `EmbeddedResource` in the generator assembly.
- **Consumed by Plan 3, Phase 3**: Build configuration to embed resources.
- **Consumed by Plan 3, Phase 5**: Standalone mode tests verify embedded source emission with visibility replacement.

#### Diagnostic Descriptors (Plan 2 public API for testing)
```csharp
namespace Logsmith.Generator.Diagnostics;
internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor LSMITH001; // placeholder no match
    internal static readonly DiagnosticDescriptor LSMITH002; // param unreferenced
    internal static readonly DiagnosticDescriptor LSMITH003; // not static partial
    internal static readonly DiagnosticDescriptor LSMITH004; // no formatting path
    internal static readonly DiagnosticDescriptor LSMITH005; // caller param in template
}
```
- **Consumed by Plan 3, Phase 5**: Generator tests assert specific diagnostics are reported.

### Plan 3 produces → Plan 1 consumes

#### Concrete Sink Classes (created by `LogConfigBuilder` convenience methods)
```csharp
namespace Logsmith.Sinks;
public class ConsoleSink : ILogSink;
public class FileSink : ILogSink, IAsyncDisposable;
public class DebugSink : ILogSink;
public class RecordingSink : ILogSink;
public class NullSink : ILogSink;
public abstract class TextLogSink : ILogSink;
public abstract class BufferedLogSink : ILogSink;
```
- **Consumed by Plan 1**: `LogConfigBuilder.AddConsoleSink()` instantiates `ConsoleSink`, `AddFileSink()` instantiates `FileSink`, `AddDebugSink()` instantiates `DebugSink`. Plan 1's `LogConfigBuilder` references these types.

> **Note**: This creates a circular compile-time dependency. Resolution: `LogConfigBuilder` convenience methods (`AddConsoleSink`, `AddFileSink`, `AddDebugSink`) are implemented in Plan 1 Phase 3 but the sink types they instantiate are defined in Plan 3 Phase 1-2. Since all sinks live in the same `Logsmith` assembly, this is an intra-project ordering concern, not a cross-assembly dependency. Plan 1 Phase 3 can reference forward-declared sink types within the same project. Both plans write to `src/Logsmith/`.

## Function/Method Signatures Crossing Boundaries

### Generated code calls (Plan 2 emits → Plan 1 provides)

```csharp
// Early-exit check
LogManager.IsEnabled(LogLevel level)

// Default dispatch (no explicit sink)
LogManager.Dispatch<TState>(in LogEntry entry, ReadOnlySpan<byte> utf8Message, TState state, WriteProperties<TState> propertyWriter)

// Explicit sink dispatch
sink.IsEnabled(LogLevel level)
sink.Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)

// Text message construction
new Utf8LogWriter(Span<byte> buffer)
writer.Write(ReadOnlySpan<byte> utf8Literal)
writer.WriteFormatted<T>(T value) where T : IUtf8SpanFormattable
writer.WriteString(string? value)
writer.GetWritten() → ReadOnlySpan<byte>

// Structured serialization
ILogStructurable.WriteStructured(Utf8JsonWriter writer)
```

### Test code calls (Plan 3 tests → Plan 1 + Plan 2 provide)

```csharp
// Runtime tests
LogManager.Initialize(Action<LogConfigBuilder> configure)
LogManager.Reconfigure(Action<LogConfigBuilder> configure)
LogConfigBuilder.AddSink(ILogSink sink)
LogConfigBuilder.MinimumLevel { get; set; }
LogConfigBuilder.SetMinimumLevel(string category, LogLevel level)
RecordingSink.Entries → List<CapturedEntry>  // Plan 3 internal

// Generator tests use Roslyn CSharpGeneratorDriver to invoke generator
```
