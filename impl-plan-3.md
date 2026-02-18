# Implementation Plan 3: Sinks, NuGet Packaging & Tests

## Coverage

This plan covers partition index items 3.1 through 3.15:

- **3.1** `TextLogSink` abstract base class
- **3.2** `BufferedLogSink` abstract base class (async-buffered pattern)
- **3.3** `NullSink` — discards everything
- **3.4** `DebugSink` — System.Diagnostics.Debug output
- **3.5** `ConsoleSink` — ANSI-colored UTF8 to stdout
- **3.6** `FileSink` — Channel\<T\> async-buffered, rolling file support, IAsyncDisposable
- **3.7** `RecordingSink` — List\<CapturedEntry\> for test assertions
- **3.8** NuGet packaging: `Logsmith` package (runtime + bundled generator)
- **3.9** NuGet packaging: `Logsmith.Generator` standalone package (analyzer-only)
- **3.10** Embedded resource build integration
- **3.11** `Logsmith.Tests` project setup
- **3.12** Runtime behavior tests
- **3.13** Sink tests
- **3.14** `Logsmith.Generator.Tests` project setup
- **3.15** Generator compilation tests

## Dependency Map

### This plan requires from other plans

| Source | What | Used by |
|--------|------|---------|
| Plan 1, Phase 1 | `ILogSink`, `IStructuredLogSink`, `LogLevel`, `LogEntry`, `WriteProperties<TState>` | Phase 1 — sink base classes implement these interfaces |
| Plan 1, Phase 2 | `Utf8LogWriter`, `SinkSet` internals | Phase 2 — `ConsoleSink`/`FileSink` use `Utf8LogWriter` for formatting; `SinkSet` shape informs sink classification |
| Plan 1, Phase 3 | `LogManager`, `LogConfigBuilder`, `LogConfig` | Phase 3 — NuGet packaging bundles full runtime; Phase 4 — tests exercise `LogManager.Initialize`, `Dispatch`, `Reconfigure` |
| Plan 2, Phase 1 | Generator project/assembly exists | Phase 3 — NuGet packaging bundles generator DLL |
| Plan 2, Phase 5 | Complete generator pipeline | Phase 5 — generator tests validate full code emission |

### This plan provides to other plans

| What | Consumer |
|------|----------|
| `ConsoleSink`, `FileSink`, `DebugSink` concrete types | Plan 1, Phase 3 — `LogConfigBuilder.AddConsoleSink()`, `AddFileSink()`, `AddDebugSink()` instantiate these (same assembly, intra-project dependency) |

---

## Phase 1: Sink Base Classes and Simple Sinks

### Entry Criteria

- Plan 1, Phase 1 complete: `ILogSink`, `IStructuredLogSink`, `LogLevel`, `LogEntry`, `WriteProperties<TState>` delegate defined in `src/Logsmith/`.

### Description

Implement the two abstract base classes (`TextLogSink`, `BufferedLogSink`) and the two simplest concrete sinks (`NullSink`, `DebugSink`). All sinks live in `src/Logsmith/Sinks/` within the `Logsmith.Sinks` namespace. These are part of the `Logsmith` project (not a separate assembly).

### Core Concepts

- **TextLogSink**: Abstract base for sinks that consume the pre-formatted UTF8 text message. Implements `ILogSink`. Provides a default `IsEnabled` that checks level against a configurable minimum. Subclasses override a single `WriteMessage` method receiving the decoded/raw text bytes.
- **BufferedLogSink**: Abstract base for sinks that need async I/O. Uses `System.Threading.Channels.Channel<T>` internally to queue log entries, with a background `Task` draining the channel. Implements `ILogSink` and `IAsyncDisposable`. Subclasses override `WriteBuffered` to perform actual I/O. Dispose completes the channel and awaits the drain task.
- **NullSink**: `IsEnabled` returns `false`. `Write` is a no-op. `Dispose` is a no-op.
- **DebugSink**: Writes to `System.Diagnostics.Debug.WriteLine`. Converts UTF8 message bytes to string for the Debug API. `IsEnabled` delegates to `Debugger.IsAttached` combined with level check.

### Signatures

```csharp
// src/Logsmith/Sinks/TextLogSink.cs
namespace Logsmith.Sinks;

public abstract class TextLogSink : ILogSink
{
    protected LogLevel MinimumLevel { get; }
    protected TextLogSink(LogLevel minimumLevel = LogLevel.Trace);
    public virtual bool IsEnabled(LogLevel level);
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
    protected abstract void WriteMessage(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
    public virtual void Dispose();
}
```

```csharp
// src/Logsmith/Sinks/BufferedLogSink.cs
namespace Logsmith.Sinks;

public abstract class BufferedLogSink : ILogSink, IAsyncDisposable
{
    // Internal buffered entry type to capture data off the hot path
    protected readonly record struct BufferedEntry(
        LogLevel Level,
        int EventId,
        long TimestampTicks,
        string Category,
        Exception? Exception,
        string? CallerFile,
        int CallerLine,
        string? CallerMember,
        byte[] Utf8Message);

    protected LogLevel MinimumLevel { get; }
    protected BufferedLogSink(LogLevel minimumLevel = LogLevel.Trace, int capacity = 1024);
    public virtual bool IsEnabled(LogLevel level);
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
    protected abstract Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct);
    public void Dispose();
    public ValueTask DisposeAsync();
}
```

```csharp
// src/Logsmith/Sinks/NullSink.cs
namespace Logsmith.Sinks;

public class NullSink : ILogSink
{
    public bool IsEnabled(LogLevel level);
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
    public void Dispose();
}
```

```csharp
// src/Logsmith/Sinks/DebugSink.cs
namespace Logsmith.Sinks;

public class DebugSink : ILogSink
{
    public DebugSink(LogLevel minimumLevel = LogLevel.Trace);
    public bool IsEnabled(LogLevel level);
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
    public void Dispose();
}
```

### Phase Deliverables

- `src/Logsmith/Sinks/TextLogSink.cs` — abstract base with level-gated `WriteMessage` hook
- `src/Logsmith/Sinks/BufferedLogSink.cs` — abstract base with Channel\<T\> buffering and background drain
- `src/Logsmith/Sinks/NullSink.cs` — no-op sink
- `src/Logsmith/Sinks/DebugSink.cs` — Debug.WriteLine sink
- All four compile against `ILogSink`/`LogLevel`/`LogEntry` from Plan 1 Phase 1

---

## Phase 2: ConsoleSink, FileSink, RecordingSink

### Entry Criteria

- Phase 1 complete (base classes available)
- Plan 1, Phase 2 complete: `Utf8LogWriter`, `SinkSet` internals defined

### Description

Implement the three remaining concrete sinks. `ConsoleSink` writes ANSI-colored UTF8 directly to stdout. `FileSink` extends `BufferedLogSink` with rolling file support. `RecordingSink` captures entries for test assertions.

### Core Concepts

- **ConsoleSink**: Extends `TextLogSink`. Writes to `Console.OpenStandardOutput()` as a raw stream. ANSI escape codes for level-based coloring (e.g., red for Error/Critical, yellow for Warning, cyan for Debug, gray for Trace). Configurable `colored` flag to disable ANSI codes. Formats output as `[TIMESTAMP LEVEL CATEGORY] message\n`.
- **FileSink**: Extends `BufferedLogSink`. Constructor takes a file path. Uses `Channel<BufferedEntry>` (from base) for async writes. Supports rolling: when a file exceeds a configurable size threshold, renames to `filename.YYYYMMDD-HHMMSS.log` and opens a new file. Implements `IAsyncDisposable` to flush the channel and close the file handle.
- **RecordingSink**: Implements `ILogSink`. Stores each write as a `CapturedEntry` in a `List<CapturedEntry>`. `CapturedEntry` is a record holding all `LogEntry` fields plus the decoded message string. Provides `Entries` property and `Clear()` method. Designed for unit test assertions.

### Signatures

```csharp
// src/Logsmith/Sinks/ConsoleSink.cs
namespace Logsmith.Sinks;

public class ConsoleSink : TextLogSink
{
    public ConsoleSink(bool colored = true, LogLevel minimumLevel = LogLevel.Trace);
    protected override void WriteMessage(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
}
```

```csharp
// src/Logsmith/Sinks/FileSink.cs
namespace Logsmith.Sinks;

public class FileSink : BufferedLogSink
{
    public FileSink(string path, LogLevel minimumLevel = LogLevel.Trace, long maxFileSizeBytes = 10 * 1024 * 1024);
    protected override Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct);
    // Rolling: internally manages FileStream, rolls when size exceeded
}
```

```csharp
// src/Logsmith/Sinks/RecordingSink.cs
namespace Logsmith.Sinks;

public class RecordingSink : ILogSink
{
    public record CapturedEntry(
        LogLevel Level,
        int EventId,
        long TimestampTicks,
        string Category,
        Exception? Exception,
        string? CallerFile,
        int CallerLine,
        string? CallerMember,
        string Message);

    public List<CapturedEntry> Entries { get; }
    public RecordingSink(LogLevel minimumLevel = LogLevel.Trace);
    public bool IsEnabled(LogLevel level);
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message);
    public void Clear();
    public void Dispose();
}
```

### Phase Deliverables

- `src/Logsmith/Sinks/ConsoleSink.cs` — ANSI-colored UTF8 console output
- `src/Logsmith/Sinks/FileSink.cs` — async-buffered rolling file sink
- `src/Logsmith/Sinks/RecordingSink.cs` — test assertion sink with `CapturedEntry` list
- All three sinks are fully functional and compile within the `Logsmith` project

---

## Phase 3: NuGet Packaging and Embedded Resource Integration

### Entry Criteria

- Phase 2 complete (all sinks implemented)
- Plan 1, Phase 3 complete: `LogManager`, `LogConfigBuilder` finalized — full runtime assembly ready
- Plan 2, Phase 1 complete: Generator assembly exists and compiles

### Description

Configure the `.csproj` files for NuGet packaging of both `Logsmith` and `Logsmith.Generator`. Set up the embedded resource build integration so that the generator assembly contains Logsmith's `.cs` source files.

### Core Concepts

- **`Logsmith` NuGet package**: Contains `lib/net10.0/Logsmith.dll` (runtime types + sinks) and `analyzers/dotnet/cs/Logsmith.Generator.dll` (source generator). A single `PackageReference` gives consumers both the runtime types and the generator. The `.csproj` uses `<None Include="..." Pack="true" PackagePath="analyzers/dotnet/cs" />` to bundle the generator DLL.
- **`Logsmith.Generator` standalone NuGet package**: Marked with `<IncludeBuildOutput>false</IncludeBuildOutput>` and ships only in `analyzers/dotnet/cs/`. Zero runtime DLLs in consumer output. Referenced via `OutputItemType="Analyzer" ReferenceOutputAssembly="false"`.
- **Embedded resource integration**: The `Logsmith.Generator.csproj` includes Logsmith's `.cs` source files as `<EmbeddedResource>` items. These files are read at generation time by `EmbeddedSourceEmitter` (Plan 2, Phase 3-4) for standalone mode emission. The build must link to the source files from `src/Logsmith/` rather than copying them.

### Algorithm: Embedded Resource Linking

The generator `.csproj` uses `<EmbeddedResource Include="../Logsmith/**/*.cs" Exclude="../Logsmith/obj/**;../Logsmith/bin/**" Link="EmbeddedSources/%(RecursiveDir)%(Filename)%(Extension)" />` to embed all Logsmith source files without physically copying them. The `Link` metadata controls the resource name used at runtime.

### Project File Changes

```xml
<!-- src/Logsmith/Logsmith.csproj additions for NuGet -->
<PropertyGroup>
    <PackageId>Logsmith</PackageId>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <IsPackable>true</IsPackable>
    <!-- Pack generator DLL into analyzers path -->
</PropertyGroup>
<ItemGroup>
    <None Include="$(OutputPath)\..\..\..\Logsmith.Generator\$(Configuration)\netstandard2.0\Logsmith.Generator.dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
</ItemGroup>
```

```xml
<!-- src/Logsmith.Generator/Logsmith.Generator.csproj additions -->
<PropertyGroup>
    <PackageId>Logsmith.Generator</PackageId>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
    <IsPackable>true</IsPackable>
    <DevelopmentDependency>true</DevelopmentDependency>
</PropertyGroup>
<ItemGroup>
    <!-- Embed Logsmith source files for standalone mode -->
    <EmbeddedResource Include="../Logsmith/**/*.cs"
                      Exclude="../Logsmith/obj/**;../Logsmith/bin/**"
                      Link="EmbeddedSources/%(RecursiveDir)%(Filename)%(Extension)" />
</ItemGroup>
<ItemGroup>
    <None Include="$(OutputPath)/Logsmith.Generator.dll"
          Pack="true"
          PackagePath="analyzers/dotnet/cs"
          Visible="false" />
</ItemGroup>
```

### Phase Deliverables

- `src/Logsmith/Logsmith.csproj` updated with NuGet packaging properties and generator DLL bundling
- `src/Logsmith.Generator/Logsmith.Generator.csproj` updated with standalone NuGet packaging, embedded resources, analyzer-only output
- `dotnet pack` produces both `Logsmith.nupkg` and `Logsmith.Generator.nupkg` with correct layout
- Embedded resources are accessible via `Assembly.GetManifestResourceNames()` in the generator assembly

---

## Phase 4: Runtime and Sink Tests

### Entry Criteria

- Phase 3 complete (packaging configured)
- Plan 1, Phase 3 complete: `LogManager.Initialize`, `Reconfigure`, `IsEnabled`, `Dispatch` implemented
- Phase 2 complete (all sinks implemented)

### Description

Create the `Logsmith.Tests` project and implement runtime behavior tests and sink-specific tests. Uses NUnit only (no FluentAssertions, no Moq). Uses `RecordingSink` as the primary test sink.

### Core Concepts

- **Test project setup**: `tests/Logsmith.Tests/Logsmith.Tests.csproj` targets `net10.0`, references `src/Logsmith/Logsmith.csproj` as a project reference, and references `NUnit`, `NUnit3TestAdapter`, `Microsoft.NET.Test.Sdk`.
- **Runtime tests**: Validate `LogManager` lifecycle (Initialize, Reconfigure), dispatch routing (text sinks vs structured sinks), level filtering (global minimum + per-category overrides), volatile config swap semantics.
- **Sink tests**: Each sink type gets targeted tests. `RecordingSink` is used to verify dispatch correctness. `ConsoleSink` tests capture stdout. `FileSink` tests use temp directories and verify file creation/rolling. `DebugSink` tests verify no-throw behavior. `NullSink` tests verify `IsEnabled` returns false and `Write` is a no-op.

### Signatures

```csharp
// tests/Logsmith.Tests/Logsmith.Tests.csproj
// <Project Sdk="Microsoft.NET.Sdk">
//   <PropertyGroup>
//     <TargetFramework>net10.0</TargetFramework>
//   </PropertyGroup>
//   <ItemGroup>
//     <ProjectReference Include="../../src/Logsmith/Logsmith.csproj" />
//     <PackageReference Include="NUnit" />
//     <PackageReference Include="NUnit3TestAdapter" />
//     <PackageReference Include="Microsoft.NET.Test.Sdk" />
//   </ItemGroup>
// </Project>
```

```csharp
// tests/Logsmith.Tests/LogManagerTests.cs
namespace Logsmith.Tests;

[TestFixture]
public class LogManagerTests
{
    [SetUp] public void SetUp();
    [TearDown] public void TearDown();

    [Test] public void Initialize_WithSink_DispatchesMessages();
    [Test] public void Initialize_MinimumLevel_FiltersBelow();
    [Test] public void Reconfigure_SwapsConfig_NewSinkReceivesMessages();
    [Test] public void Reconfigure_OldSinkStopsReceiving();
    [Test] public void IsEnabled_BelowMinimum_ReturnsFalse();
    [Test] public void IsEnabled_AtMinimum_ReturnsTrue();
    [Test] public void IsEnabled_AboveMinimum_ReturnsTrue();
    [Test] public void CategoryOverride_OverridesGlobalMinimum();
    [Test] public void Dispatch_TextSink_ReceivesUtf8Message();
    [Test] public void Dispatch_StructuredSink_ReceivesStructuredCall();
    [Test] public void Dispatch_MultipleSinks_AllReceiveMessage();
}
```

```csharp
// tests/Logsmith.Tests/SinkTests/NullSinkTests.cs
namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class NullSinkTests
{
    [Test] public void IsEnabled_AlwaysReturnsFalse();
    [Test] public void Write_DoesNotThrow();
    [Test] public void Dispose_DoesNotThrow();
}
```

```csharp
// tests/Logsmith.Tests/SinkTests/RecordingSinkTests.cs
namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class RecordingSinkTests
{
    [Test] public void Write_CapturesEntry();
    [Test] public void Write_CapturesUtf8MessageAsString();
    [Test] public void Write_MultipleEntries_AllCaptured();
    [Test] public void Clear_RemovesAllEntries();
    [Test] public void IsEnabled_BelowMinimum_ReturnsFalse();
    [Test] public void IsEnabled_AtOrAboveMinimum_ReturnsTrue();
    [Test] public void CapturedEntry_ContainsAllLogEntryFields();
}
```

```csharp
// tests/Logsmith.Tests/SinkTests/ConsoleSinkTests.cs
namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class ConsoleSinkTests
{
    [Test] public void Write_OutputsToStdout();
    [Test] public void Write_Colored_IncludesAnsiCodes();
    [Test] public void Write_NotColored_NoAnsiCodes();
    [Test] public void IsEnabled_RespectsMinimumLevel();
}
```

```csharp
// tests/Logsmith.Tests/SinkTests/FileSinkTests.cs
namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class FileSinkTests
{
    [Test] public Task Write_CreatesFileAndWritesContent();
    [Test] public Task Write_ExceedsMaxSize_RollsFile();
    [Test] public Task DisposeAsync_FlushesRemainingEntries();
    [Test] public void IsEnabled_RespectsMinimumLevel();
}
```

```csharp
// tests/Logsmith.Tests/SinkTests/DebugSinkTests.cs
namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class DebugSinkTests
{
    [Test] public void Write_DoesNotThrow();
    [Test] public void IsEnabled_RespectsMinimumLevel();
    [Test] public void Dispose_DoesNotThrow();
}
```

### Phase Deliverables

- `tests/Logsmith.Tests/Logsmith.Tests.csproj` created and configured
- `tests/Logsmith.Tests/LogManagerTests.cs` — full runtime behavior test suite
- `tests/Logsmith.Tests/SinkTests/NullSinkTests.cs`
- `tests/Logsmith.Tests/SinkTests/RecordingSinkTests.cs`
- `tests/Logsmith.Tests/SinkTests/ConsoleSinkTests.cs`
- `tests/Logsmith.Tests/SinkTests/FileSinkTests.cs`
- `tests/Logsmith.Tests/SinkTests/DebugSinkTests.cs`
- All tests pass via `dotnet test`

---

## Phase 5: Generator Compilation Tests

### Entry Criteria

- Phase 4 complete (runtime tests pass)
- Phase 3 complete (NuGet packaging and embedded resources configured)
- Plan 2, Phase 5 complete: Full generator pipeline implemented (all emission, diagnostics, standalone mode)

### Description

Create the `Logsmith.Generator.Tests` project and implement generator compilation tests using Roslyn's `CSharpGeneratorDriver` infrastructure. Tests validate parameter classification, template parsing, diagnostic emission, code emission correctness, standalone mode behavior, and conditional compilation.

### Core Concepts

- **Test project setup**: `tests/Logsmith.Generator.Tests/Logsmith.Generator.Tests.csproj` targets `net10.0`, references the generator project, and includes `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.CSharp.Workspaces` for the Roslyn test infrastructure.
- **CSharpGeneratorDriver pattern**: Each test creates a `CSharpCompilation` with inline source code, runs the generator via `CSharpGeneratorDriver.Create(new LogsmithGenerator()).RunGenerators(compilation)`, then inspects the `GeneratorRunResult` for generated sources and diagnostics.
- **Parameter classification tests**: Verify that sink, exception, caller, and message parameters are correctly classified.
- **Template parsing tests**: Verify placeholder extraction, case-insensitive matching, template-free auto-generation.
- **Diagnostic tests**: Assert that LSMITH001-005 are reported for the correct invalid inputs.
- **Code emission tests**: Verify generated method bodies contain correct `Utf8LogWriter` usage, `LogManager.Dispatch` calls, nullable guards, explicit sink routing, and type serialization priority chain.
- **Standalone mode tests**: Verify that when `Logsmith.LogLevel` is absent from the compilation, the generator emits embedded source with `public` replaced by `internal`.
- **Conditional compilation tests**: Verify `[Conditional("DEBUG")]` is emitted based on `LogsmithConditionalLevel` threshold, and `AlwaysEmit = true` bypasses it.

### Signatures

```csharp
// tests/Logsmith.Generator.Tests/Logsmith.Generator.Tests.csproj
// <Project Sdk="Microsoft.NET.Sdk">
//   <PropertyGroup>
//     <TargetFramework>net10.0</TargetFramework>
//   </PropertyGroup>
//   <ItemGroup>
//     <ProjectReference Include="../../src/Logsmith.Generator/Logsmith.Generator.csproj"
//                       OutputItemType="Analyzer"
//                       ReferenceOutputAssembly="true" />
//     <ProjectReference Include="../../src/Logsmith/Logsmith.csproj" />
//     <PackageReference Include="NUnit" />
//     <PackageReference Include="NUnit3TestAdapter" />
//     <PackageReference Include="Microsoft.NET.Test.Sdk" />
//     <PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
//   </ItemGroup>
// </Project>
```

```csharp
// tests/Logsmith.Generator.Tests/GeneratorTestHelper.cs
namespace Logsmith.Generator.Tests;

internal static class GeneratorTestHelper
{
    // Creates a CSharpCompilation from source strings with Logsmith references
    internal static CSharpCompilation CreateCompilation(params string[] sources);

    // Runs the generator and returns the result
    internal static GeneratorRunResult RunGenerator(
        CSharpCompilation compilation,
        AnalyzerConfigOptionsProvider? options = null);

    // Creates an AnalyzerConfigOptionsProvider with LogsmithConditionalLevel set
    internal static AnalyzerConfigOptionsProvider CreateOptionsWithConditionalLevel(string level);
}
```

```csharp
// tests/Logsmith.Generator.Tests/ParameterClassificationTests.cs
namespace Logsmith.Generator.Tests;

[TestFixture]
public class ParameterClassificationTests
{
    [Test] public void FirstParam_ILogSink_ClassifiedAsSink();
    [Test] public void ExceptionParam_ClassifiedAsException();
    [Test] public void CallerFilePath_ClassifiedAsCallerFile();
    [Test] public void CallerLineNumber_ClassifiedAsCallerLine();
    [Test] public void CallerMemberName_ClassifiedAsCallerMember();
    [Test] public void RegularParam_ClassifiedAsMessageParam();
    [Test] public void MixedParams_AllClassifiedCorrectly();
}
```

```csharp
// tests/Logsmith.Generator.Tests/TemplateParsingTests.cs
namespace Logsmith.Generator.Tests;

[TestFixture]
public class TemplateParsingTests
{
    [Test] public void ExplicitTemplate_ExtractsPlaceholders();
    [Test] public void TemplateFree_GeneratesFromMethodNameAndParams();
    [Test] public void PlaceholderMatching_CaseInsensitive();
    [Test] public void EmptyTemplate_UsesTemplateFreeMode();
}
```

```csharp
// tests/Logsmith.Generator.Tests/DiagnosticTests.cs
namespace Logsmith.Generator.Tests;

[TestFixture]
public class DiagnosticTests
{
    [Test] public void LSMITH001_PlaceholderNoMatchingParam();
    [Test] public void LSMITH002_ParamNotInTemplate();
    [Test] public void LSMITH003_NotStaticPartialInPartialClass();
    [Test] public void LSMITH004_NoSupportedFormattingPath();
    [Test] public void LSMITH005_CallerParamNameInTemplate();
}
```

```csharp
// tests/Logsmith.Generator.Tests/CodeEmissionTests.cs
namespace Logsmith.Generator.Tests;

[TestFixture]
public class CodeEmissionTests
{
    [Test] public void TextPath_UsesUtf8LogWriter();
    [Test] public void TextPath_FormattingPriorityChain();
    [Test] public void StructuredPath_UsesUtf8JsonWriter();
    [Test] public void StructuredPath_ILogStructurable_CallsWriteStructured();
    [Test] public void NullableValueType_EmitsHasValueGuard();
    [Test] public void NullableRefType_EmitsIsNotNullGuard();
    [Test] public void ExplicitSink_RoutesToSinkDirectly();
    [Test] public void DefaultDispatch_CallsLogManager();
    [Test] public void CallerInfo_PassedToLogEntry();
    [Test] public void CallerInfo_AnyOrderCombination();
    [Test] public void EventId_AutoGenerated_StableHash();
    [Test] public void EventId_UserOverride_UsesProvidedValue();
}
```

```csharp
// tests/Logsmith.Generator.Tests/StandaloneModeTests.cs
namespace Logsmith.Generator.Tests;

[TestFixture]
public class StandaloneModeTests
{
    [Test] public void StandaloneMode_EmitsEmbeddedSources();
    [Test] public void StandaloneMode_PublicReplacedWithInternal();
    [Test] public void SharedMode_DoesNotEmitEmbeddedSources();
    [Test] public void ModeDetection_LogLevelPresent_SharedMode();
    [Test] public void ModeDetection_LogLevelAbsent_StandaloneMode();
}
```

```csharp
// tests/Logsmith.Generator.Tests/ConditionalCompilationTests.cs
namespace Logsmith.Generator.Tests;

[TestFixture]
public class ConditionalCompilationTests
{
    [Test] public void DefaultThreshold_Debug_TraceAndDebugGetConditional();
    [Test] public void Threshold_Information_TraceDebugInfoGetConditional();
    [Test] public void Threshold_None_NoMethodsGetConditional();
    [Test] public void AlwaysEmit_BypassesConditional();
    [Test] public void AboveThreshold_NoConditionalAttribute();
}
```

### Phase Deliverables

- `tests/Logsmith.Generator.Tests/Logsmith.Generator.Tests.csproj` created and configured
- `tests/Logsmith.Generator.Tests/GeneratorTestHelper.cs` — shared Roslyn test infrastructure
- `tests/Logsmith.Generator.Tests/ParameterClassificationTests.cs`
- `tests/Logsmith.Generator.Tests/TemplateParsingTests.cs`
- `tests/Logsmith.Generator.Tests/DiagnosticTests.cs`
- `tests/Logsmith.Generator.Tests/CodeEmissionTests.cs`
- `tests/Logsmith.Generator.Tests/StandaloneModeTests.cs`
- `tests/Logsmith.Generator.Tests/ConditionalCompilationTests.cs`
- All generator tests pass via `dotnet test`
- Full test suite (`Logsmith.Tests` + `Logsmith.Generator.Tests`) passes
