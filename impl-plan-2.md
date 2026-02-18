# Implementation Plan 2: Source Generator (`src/Logsmith.Generator/`)

## Coverage

This plan covers spec sections 2.1 through 2.17 as assigned in `impl-partition-index.md`:

| # | Feature |
|---|---------|
| 2.1 | Project setup (`Logsmith.Generator.csproj`, `netstandard2.0`, analyzer output) |
| 2.2 | `IncrementalGenerator` pipeline (syntax/semantic providers, MSBuild options) |
| 2.3 | Parameter classification algorithm |
| 2.4 | Template parsing (`{placeholder}` extraction, case-insensitive matching) |
| 2.5 | Template-free mode (auto-generated message from method name + params) |
| 2.6 | Diagnostics LSMITH001, LSMITH002, LSMITH003, LSMITH005 |
| 2.7 | EventId auto-generation (stable hash, user override) |
| 2.8 | Conditional compilation (`LogsmithConditionalLevel`, `[Conditional("DEBUG")]`, AlwaysEmit) |
| 2.9 | Mode detection (shared vs standalone) |
| 2.10 | Embedded source emission (EmbeddedResource read, visibility replacement) |
| 2.11 | Code emission: text path (Utf8LogWriter, type serialization priority chain) |
| 2.12 | Code emission: structured path (Utf8JsonWriter, ILogStructurable, WriteProperties) |
| 2.13 | Code emission: nullable handling (`HasValue`, `is not null`, null literals) |
| 2.14 | Code emission: caller info parameters |
| 2.15 | Code emission: explicit sink vs LogManager dispatch |
| 2.16 | Diagnostic LSMITH004 (no supported formatting path) |
| 2.17 | Full method body assembly |

## Dependency Map

### Requires from other plans

| Source | What | Why |
|--------|------|-----|
| `impl-plan-1.md` Phase 1 | `LogLevel`, `LogEntry`, `LogMessageAttribute`, `LogCategoryAttribute`, `ILogSink`, `IStructuredLogSink`, `ILogStructurable`, `WriteProperties<TState>` | Generator performs semantic analysis against these types; needs their metadata names and shapes for symbol comparison |
| `impl-plan-1.md` Phase 2 | `Utf8LogWriter` (ref struct shape, method signatures), `SinkSet` (dual-array shape), `LogConfig` | Code emission references `Utf8LogWriter` methods; emitted dispatch code targets the text/structured sink split |
| `impl-plan-1.md` Phase 3 | `LogManager.IsEnabled`, `LogManager.Dispatch<TState>` signatures | Emitted method bodies call these for the default (non-explicit-sink) path |

### Provides to other plans

| Consumer | What | Why |
|----------|------|-----|
| `impl-plan-3.md` Phase 3 | `Logsmith.Generator.dll` assembly | NuGet packaging bundles into `analyzers/dotnet/cs/` |
| `impl-plan-3.md` Phase 3 | Embedded `.cs` resources in generator assembly | Build config for embedding Plan 1 source files |
| `impl-plan-3.md` Phase 5 | `DiagnosticDescriptors` (LSMITH001-005), full generator pipeline | Generator tests invoke via Roslyn `CSharpGeneratorDriver` |

## Phase 1: Project Setup and Generator Pipeline Skeleton

### Entry Criteria
- Plan 1 Phase 1 complete (Logsmith.csproj exists with attribute and enum types).

### Description
Create the `Logsmith.Generator` project targeting `netstandard2.0` with Roslyn analyzer output type. Define the `IIncrementalGenerator` entry point with empty provider registrations. Establish the project file structure under `src/Logsmith.Generator/`.

### Core Concepts
- **Analyzer project constraints**: Must target `netstandard2.0`. Must set `<IsRoslynComponent>true</IsRoslynComponent>`. References `Microsoft.CodeAnalysis.CSharp` (>= 4.3.0). No dependency on the Logsmith runtime assembly (types are resolved by metadata name from the consumer's compilation).
- **Incremental generator**: Implements `IIncrementalGenerator` with `Initialize(IncrementalGeneratorInitializationContext)`. Uses `SyntaxProvider.ForAttributeWithMetadataName` to find methods decorated with `LogMessageAttribute`.
- **MSBuild property access**: Reads `LogsmithConditionalLevel` via `AnalyzerConfigOptionsProvider.GlobalOptions.TryGetValue("build_property.LogsmithConditionalLevel", ...)`.

### Signatures

```csharp
// src/Logsmith.Generator/Logsmith.Generator.csproj
// TargetFramework: netstandard2.0
// IsRoslynComponent: true
// PackageReference: Microsoft.CodeAnalysis.CSharp >= 4.3.0

namespace Logsmith.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class LogsmithGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context);
}
```

```csharp
namespace Logsmith.Generator.Models;

public enum ParameterKind
{
    MessageParam,
    Sink,
    Exception,
    CallerFile,
    CallerLine,
    CallerMember
}
```

```csharp
namespace Logsmith.Generator.Models;

// Immutable model representing a classified method parameter.
public sealed class ParameterInfo
{
    public string Name { get; }
    public string TypeFullName { get; }
    public ParameterKind Kind { get; }
    public bool IsNullableValueType { get; }
    public bool IsNullableReferenceType { get; }
    public bool HasDefaultValue { get; }
    public object? DefaultValue { get; }
}
```

```csharp
namespace Logsmith.Generator.Models;

// Represents a single segment of a parsed template: either a literal or a placeholder.
public sealed class TemplatePart
{
    public bool IsPlaceholder { get; }
    public string Text { get; }               // literal text or placeholder name
    public ParameterInfo? BoundParameter { get; } // set during binding for placeholders
}
```

```csharp
namespace Logsmith.Generator.Models;

// Complete model for one log method, produced by the pipeline and consumed by emitters.
public sealed class LogMethodInfo
{
    public string ContainingNamespace { get; }
    public string ContainingClassName { get; }
    public string MethodName { get; }
    public string Category { get; }           // from [LogCategory] or class name
    public LogLevel Level { get; }            // mirrored enum value
    public int EventId { get; }               // user-specified or auto-hash
    public bool AlwaysEmit { get; }
    public string? TemplateString { get; }    // null = template-free mode
    public IReadOnlyList<ParameterInfo> Parameters { get; }
    public IReadOnlyList<TemplatePart> TemplateParts { get; }
    public bool HasExplicitSink { get; }
    public bool IsStandaloneMode { get; }
    public string ConditionalLevel { get; }   // from MSBuild property
    public Location MethodLocation { get; }
}
```

### Deliverables
- `src/Logsmith.Generator/Logsmith.Generator.csproj` compiles and produces an analyzer assembly.
- `LogsmithGenerator` class registered as `[Generator]` with pipeline skeleton (no code emitted yet).
- All model types (`ParameterKind`, `ParameterInfo`, `TemplatePart`, `LogMethodInfo`) defined.
- Solution file updated with generator project.

---

## Phase 2: Parameter Classification, Template Parsing, and Diagnostics

### Entry Criteria
- Phase 1 complete (generator project compiles, model types exist).

### Description
Implement the parameter classification algorithm, template parser, template-free mode, and diagnostics LSMITH001, LSMITH002, LSMITH003, LSMITH005. Wire these into the generator pipeline so that each candidate method is fully analyzed and validated.

### Core Concepts

**Parameter classification** inspects each method parameter's type and attributes to assign a `ParameterKind`:
1. If the parameter type symbol implements or is `ILogSink` -> `Sink` (only valid as first parameter).
2. If the type is or derives from `System.Exception` -> `Exception`.
3. If the parameter has `[CallerFilePath]` attribute -> `CallerFile`.
4. If the parameter has `[CallerLineNumber]` attribute -> `CallerLine`.
5. If the parameter has `[CallerMemberName]` attribute -> `CallerMember`.
6. Otherwise -> `MessageParam`.

Classification is by attribute/type identity, not position. Only `MessageParam` parameters participate in template matching.

**Template parsing** extracts `{placeholderName}` tokens from the message string. Each segment between placeholders is a literal `TemplatePart`. Each placeholder is bound to a `MessageParam` parameter by case-insensitive name match.

**Template-free mode** (empty message string): auto-generate template parts as `"MethodName param1={param1} param2={param2}"` using the method name and all `MessageParam` parameter names. Every `MessageParam` is automatically bound.

**Diagnostics**:
- LSMITH001 (Error): A `{placeholder}` in the template has no matching `MessageParam` parameter.
- LSMITH002 (Warning): A `MessageParam` parameter is not referenced by any placeholder in the template.
- LSMITH003 (Error): The method is not `static partial` or its containing type is not `partial`.
- LSMITH005 (Warning): A caller-attributed parameter's name also appears as a `{placeholder}` in the template.

### Algorithm Details

**Template parsing algorithm**:
1. Scan message string left-to-right.
2. On `{`, begin placeholder capture. On `}`, end capture. Literal text between placeholders forms literal parts.
3. For each placeholder name, search `MessageParam` list with `StringComparer.OrdinalIgnoreCase`.
4. Report LSMITH001 if no match found. Report LSMITH005 if the name matches a caller-info parameter.
5. After all placeholders are bound, iterate unbound `MessageParam` parameters and report LSMITH002 for each.

**LSMITH003 validation**: Check `IMethodSymbol.IsStatic`, `IMethodSymbol.IsPartialDefinition`, and verify the containing type has `partial` modifier via `DeclaringSyntaxReferences`.

### Signatures

```csharp
namespace Logsmith.Generator.Parsing;

internal static class ParameterClassifier
{
    /// Classifies all parameters of a log method.
    /// Returns a list of ParameterInfo with Kind assigned.
    internal static IReadOnlyList<ParameterInfo> Classify(
        IMethodSymbol method,
        Compilation compilation);
}
```

```csharp
namespace Logsmith.Generator.Parsing;

internal static class TemplateParser
{
    /// Parses a template string into literal and placeholder parts.
    /// Returns unbound parts; binding happens in a second pass.
    internal static IReadOnlyList<TemplatePart> Parse(string template);

    /// Binds placeholder parts to classified MessageParam parameters.
    /// Populates BoundParameter on each placeholder TemplatePart.
    /// Returns diagnostics for unmatched placeholders (LSMITH001),
    /// unreferenced params (LSMITH002), and caller-param collisions (LSMITH005).
    internal static IReadOnlyList<Diagnostic> Bind(
        IReadOnlyList<TemplatePart> parts,
        IReadOnlyList<ParameterInfo> parameters,
        Location location);

    /// Generates template-free parts from method name and MessageParam parameters.
    /// Format: "MethodName param1={param1} param2={param2}"
    internal static IReadOnlyList<TemplatePart> GenerateTemplateFree(
        string methodName,
        IReadOnlyList<ParameterInfo> messageParams);
}
```

```csharp
namespace Logsmith.Generator.Diagnostics;

internal static class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor LSMITH001; // placeholder no matching param
    internal static readonly DiagnosticDescriptor LSMITH002; // param unreferenced in template
    internal static readonly DiagnosticDescriptor LSMITH003; // not static partial in partial class
    internal static readonly DiagnosticDescriptor LSMITH005; // caller param name in template
}
```

### Deliverables
- `ParameterClassifier.Classify` correctly assigns `ParameterKind` to all parameters.
- `TemplateParser.Parse` and `TemplateParser.Bind` produce bound template parts.
- `TemplateParser.GenerateTemplateFree` produces auto-generated template parts.
- All four diagnostics (LSMITH001, LSMITH002, LSMITH003, LSMITH005) defined and reported.
- Generator pipeline wired: syntax filter -> semantic transform -> classification + parsing + validation.

---

## Phase 3: EventId, Conditional Compilation, Mode Detection, and Embedded Source Emission

### Entry Criteria
- Phase 2 complete (parameter classification, template parsing, diagnostics operational).
- Plan 1 Phase 2 complete (Utf8LogWriter, SinkSet, LogConfig shapes finalized for mode detection context).

### Description
Implement EventId auto-generation, conditional compilation logic, shared/standalone mode detection, and embedded source emission. These are all pre-emission concerns that finalize the `LogMethodInfo` model before code generation.

### Core Concepts

**EventId auto-generation**: Compute a stable `int` hash of `"ClassName.MethodName"`. If the user sets `EventId` on the attribute to a nonzero value, use that instead. The hash algorithm must be deterministic across builds and platforms.

**Conditional compilation**: Read `LogsmithConditionalLevel` from `AnalyzerConfigOptionsProvider.GlobalOptions`. Map the string value to a `LogLevel` ordinal (default: `Debug` = ordinal 1). If the method's level ordinal <= threshold ordinal and `AlwaysEmit` is false, mark the method for `[Conditional("DEBUG")]` emission.

**Mode detection**: Call `compilation.GetTypeByMetadataName("Logsmith.LogLevel")`. If found and `symbol.ContainingAssembly.Name == "Logsmith"`, the consumer has a runtime reference -> shared mode. Otherwise -> standalone mode.

**Embedded source emission**: In standalone mode, read `.cs` files from the generator assembly's embedded resources. For each file, replace visibility keywords at type declaration sites (`public enum ` -> `internal enum `, `public interface ` -> `internal interface `, etc. for all type declaration forms listed in the spec). Add each transformed source to `SourceProductionContext.AddSource`.

### Algorithm Details

**Stable hash for EventId**:
1. Concatenate `"ClassName.MethodName"` as a UTF-16 string.
2. Apply a deterministic hash (e.g., FNV-1a 32-bit over the UTF-16 chars).
3. Return the `int` result. Collisions are acceptable; the hash is a convenience default.

**Visibility replacement patterns** (string replacements on embedded source text):
- `"public enum "` -> `"internal enum "`
- `"public interface "` -> `"internal interface "`
- `"public struct "` -> `"internal struct "`
- `"public ref struct "` -> `"internal ref struct "`
- `"public sealed class "` -> `"internal sealed class "`
- `"public static class "` -> `"internal static class "`
- `"public class "` -> `"internal class "`
- `"public delegate "` -> `"internal delegate "`
- `"public readonly struct "` -> `"internal readonly struct "`

### Signatures

```csharp
namespace Logsmith.Generator;

internal static class EventIdGenerator
{
    /// Returns stable FNV-1a hash of "ClassName.MethodName", or the user-specified
    /// eventId if nonzero.
    internal static int Generate(string className, string methodName, int userSpecified);
}
```

```csharp
namespace Logsmith.Generator;

internal static class ConditionalCompilation
{
    /// Determines if the method should receive [Conditional("DEBUG")].
    /// thresholdLevel is parsed from MSBuild property (default: Debug).
    internal static bool ShouldApplyConditional(
        LogLevel methodLevel,
        LogLevel thresholdLevel,
        bool alwaysEmit);

    /// Parses the MSBuild property string to a LogLevel ordinal.
    /// Returns LogLevel.Debug if the value is null, empty, or unrecognized.
    internal static LogLevel ParseThreshold(string? msbuildValue);
}
```

```csharp
namespace Logsmith.Generator;

internal static class ModeDetector
{
    /// Returns true if Logsmith runtime types are present in the compilation
    /// from the Logsmith assembly (shared mode). False = standalone mode.
    internal static bool IsSharedMode(Compilation compilation);
}
```

```csharp
namespace Logsmith.Generator.Emission;

internal static class EmbeddedSourceEmitter
{
    /// Reads all embedded .cs resources from the generator assembly,
    /// performs visibility replacement, and adds to context.
    internal static void EmitEmbeddedSources(SourceProductionContext context);

    /// Replaces public type declarations with internal.
    internal static string ReplaceVisibility(string source);
}
```

### Deliverables
- `EventIdGenerator.Generate` produces stable hashes or passes through user overrides.
- `ConditionalCompilation` correctly parses thresholds and determines `[Conditional]` applicability.
- `ModeDetector.IsSharedMode` distinguishes shared from standalone mode.
- `EmbeddedSourceEmitter` reads resources and performs visibility replacement.
- `LogMethodInfo` model fully populated with EventId, conditional flag, and mode.

---

## Phase 4: Code Emission (Text, Structured, Nullable, Caller Info, Sink Routing, LSMITH004)

### Entry Criteria
- Phase 3 complete (all `LogMethodInfo` fields populated).
- Plan 1 Phase 2 complete (Utf8LogWriter API shape finalized).
- Plan 1 Phase 3 complete (LogManager.Dispatch signature finalized).

### Description
Implement the code emitters that produce the method body fragments: text path (Utf8LogWriter), structured path (Utf8JsonWriter + WriteProperties delegate), nullable handling, caller info parameter passing, explicit sink vs LogManager dispatch routing, and diagnostic LSMITH004. Each emitter produces a string fragment; assembly into a complete method happens in Phase 5.

### Core Concepts

**Text path emission**: For each `TemplatePart`, emit either a `writer.Write("literal"u8)` call (for literals) or a typed write call based on the parameter's type serialization priority chain:
1. `IUtf8SpanFormattable` -> `writer.WriteFormatted(param)`
2. `ISpanFormattable` -> `writer.WriteString(param.ToString(null, null))` (stack char buffer + transcode in Utf8LogWriter)
3. `IFormattable` -> `writer.WriteString(param.ToString(null, null))`
4. Has `ToString()` override -> `writer.WriteString(param.ToString())`
5. None of the above -> report LSMITH004.

The priority chain is evaluated at compile time using the semantic model (interface implementation checks on the type symbol).

**Structured path emission**: Emit a static method matching the `WriteProperties<TState>` delegate shape. For each `MessageParam`, write a JSON property. If the parameter type implements `ILogStructurable`, call `param.WriteStructured(writer)` to write a nested object. Otherwise, write the text representation as a JSON string value.

**Nullable handling**: For each nullable parameter:
- Nullable value type (`T?`): Emit `if (param.HasValue) { /* write param.Value */ } else { /* write null */ }`
- Nullable reference type: Emit `if (param is not null) { /* write param */ } else { /* write null */ }`
- Text null: `writer.Write("null"u8)`
- Structured null: `writer.WriteNull("paramName")`

**Caller info**: Detect parameters classified as `CallerFile`, `CallerLine`, `CallerMember`. Pass their values to the `LogEntry` constructor in the emitted code. Any combination/order is valid.

**Explicit sink routing**: If `HasExplicitSink` is true, emit `sink.IsEnabled(level)` + `sink.Write(...)` instead of `LogManager.IsEnabled(level)` + `LogManager.Dispatch(...)`.

**LSMITH004**: Reported when a `MessageParam` type does not satisfy any step in the serialization priority chain and is not `string`.

### Algorithm Details

**Type serialization priority chain resolution** (compile-time, per parameter type symbol):
1. Check `ITypeSymbol.AllInterfaces` for `System.IUtf8SpanFormattable`.
2. Check for `System.ISpanFormattable`.
3. Check for `System.IFormattable`.
4. Check if type is `string` (always writable via `WriteString`).
5. Check if type has a non-inherited `ToString()` override (search members).
6. If none match, report LSMITH004.

**Structured emission for ILogStructurable**:
1. Check if parameter type implements `Logsmith.ILogStructurable`.
2. If yes: `writer.WritePropertyName("paramName"); param.WriteStructured(writer);`
3. If no: `writer.WriteString("paramName", textRepresentation);`

### Signatures

```csharp
namespace Logsmith.Generator.Emission;

internal static class TextPathEmitter
{
    /// Emits the text path code fragment: Utf8LogWriter construction,
    /// template segment writes, and GetWritten() call.
    /// Returns the code string for the text path block.
    internal static string Emit(LogMethodInfo method);

    /// Determines the serialization strategy for a parameter type.
    /// Returns the SerializationKind or null if unsupported (LSMITH004).
    internal static SerializationKind? ResolveSerializationKind(
        ITypeSymbol type,
        Compilation compilation);
}

internal enum SerializationKind
{
    Utf8SpanFormattable,
    SpanFormattable,
    Formattable,
    String,
    ToString
}
```

```csharp
namespace Logsmith.Generator.Emission;

internal static class StructuredPathEmitter
{
    /// Emits the WriteProperties static method for the structured path.
    /// Returns the code string for the static delegate method.
    internal static string EmitWritePropertiesMethod(LogMethodInfo method);

    /// Emits a single property write for one parameter.
    /// Handles ILogStructurable detection and fallback to text.
    internal static string EmitPropertyWrite(ParameterInfo param, bool isStructurable);
}
```

```csharp
namespace Logsmith.Generator.Emission;

internal static class NullableEmitter
{
    /// Wraps a write expression with a null guard appropriate to the parameter's
    /// nullability (HasValue for T?, is not null for reference types).
    /// Returns the guarded code block.
    internal static string EmitNullGuard(
        ParameterInfo param,
        string writeExpression,
        string nullExpression);
}
```

```csharp
namespace Logsmith.Generator.Diagnostics;

// Addition to existing DiagnosticDescriptors:
internal static partial class DiagnosticDescriptors
{
    internal static readonly DiagnosticDescriptor LSMITH004; // no supported formatting path
}
```

### Deliverables
- `TextPathEmitter` produces correct Utf8LogWriter code for all template parts, respecting the serialization priority chain.
- `StructuredPathEmitter` produces WriteProperties delegate methods with ILogStructurable support.
- `NullableEmitter` wraps write expressions with correct null guards for both nullable value and reference types.
- Explicit sink routing emits `sink.Write(...)` path; default routing emits `LogManager.Dispatch(...)` path.
- Caller info parameters correctly populate `LogEntry` fields.
- LSMITH004 reported for parameter types with no formatting path.

---

## Phase 5: Full Method Body Assembly and Final Emission

### Entry Criteria
- Phase 4 complete (all emitter fragments functional).
- Plan 1 Phase 3 complete (LogManager fully defined — needed for dispatch call shape).

### Description
Combine all emission pieces into complete partial method implementations. For each validated `LogMethodInfo`, assemble the full method body: conditional attribute, LogEntry construction, IsEnabled early-exit, text path block, structured dispatch (or explicit sink call), and emit via `SourceProductionContext.AddSource`. Handle standalone mode by also emitting embedded sources.

### Core Concepts

**Method body structure** (default dispatch path):
1. Optional `[Conditional("DEBUG")]` attribute.
2. `LogManager.IsEnabled(level)` early-exit guard.
3. `LogEntry` construction (level literal, eventId literal, `DateTime.UtcNow.Ticks`, category string literal, exception param or null, caller params or defaults).
4. Text path: `Span<byte> buffer = stackalloc byte[N]` + Utf8LogWriter usage + `GetWritten()`.
5. Structured state type (a readonly ref struct capturing all MessageParam values).
6. `LogManager.Dispatch(in entry, utf8Message, state, WritePropertiesMethod)`.

**Method body structure** (explicit sink path):
1. Same conditional attribute.
2. `sink.IsEnabled(level)` early-exit guard.
3. Same LogEntry construction.
4. Text path only: `sink.Write(in entry, utf8Message)`.
5. If sink also implements `IStructuredLogSink`, optionally call `WriteStructured` (detected at compile time).

**File organization**: Each containing class gets one generated source file. The file declares the partial class in the correct namespace and contains all generated method bodies for that class.

**Source hint naming**: Use `"{Namespace}.{ClassName}.g.cs"` as the hint name for `AddSource`.

### Algorithm Details

**Buffer size estimation**: Sum of literal byte lengths + estimated formatted value sizes (conservative: 32 bytes per numeric param, 128 per string param). Capped at a reasonable maximum (e.g., 4096). Overflow handled by Utf8LogWriter internally.

**State struct emission**: For structured dispatch, emit a `readonly ref struct` capturing all `MessageParam` values by reference or value. This struct is the `TState` generic argument to `Dispatch`. The `WriteProperties` static method receives this struct.

**Pipeline orchestration in generator**:
1. `ForAttributeWithMetadataName` filters methods with `LogMessageAttribute`.
2. Transform: extract semantic info -> classify params -> parse/bind template -> compute EventId -> detect mode -> build `LogMethodInfo`.
3. Combine with `AnalyzerConfigOptionsProvider` for conditional level.
4. Group by containing class.
5. For standalone mode: register post-init callback to emit embedded sources.
6. For each class group: emit partial class source file with all method bodies.

### Signatures

```csharp
namespace Logsmith.Generator.Emission;

internal static class MethodEmitter
{
    /// Assembles and returns the complete generated source for one partial class,
    /// containing all log method implementations.
    internal static string EmitClassFile(
        string namespaceName,
        string className,
        IReadOnlyList<LogMethodInfo> methods);

    /// Assembles the complete body for a single log method.
    internal static string EmitMethodBody(LogMethodInfo method);

    /// Emits the LogEntry construction expression.
    internal static string EmitLogEntryConstruction(LogMethodInfo method);

    /// Emits the state ref struct declaration for structured dispatch.
    internal static string EmitStateStruct(LogMethodInfo method);

    /// Computes the stackalloc buffer size for the text path.
    internal static int EstimateBufferSize(LogMethodInfo method);
}
```

```csharp
// Updated generator entry point with full pipeline wiring:
namespace Logsmith.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class LogsmithGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context);

    // Internal pipeline stages (not public API, shown for plan clarity):
    // - SyntaxProvider.ForAttributeWithMetadataName("Logsmith.LogMessageAttribute", ...)
    // - Transform: (context, ct) => BuildLogMethodInfo(context)
    // - Combine with GlobalOptions for ConditionalLevel
    // - GroupBy containing class
    // - RegisterSourceOutput: emit per-class files
    // - RegisterPostInitializationOutput: emit embedded sources (standalone mode)
}
```

### Deliverables
- `MethodEmitter.EmitClassFile` produces compilable partial class source files.
- `MethodEmitter.EmitMethodBody` assembles all fragments (conditional attr, entry construction, text path, structured path, dispatch).
- State ref struct emitted for structured dispatch generic parameter.
- Full pipeline wired: syntax filtering -> semantic analysis -> model building -> emission -> `AddSource`.
- Standalone mode emits embedded infrastructure sources with visibility replacement.
- All five diagnostics (LSMITH001-005) reported at appropriate pipeline stages.
- Generator produces correct, compilable output for all method shapes described in the spec (template mode, template-free mode, explicit sink, caller info, nullable params).
