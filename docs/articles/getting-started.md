# Getting Started

This guide covers installing Logsmith and writing your first log methods.

## Installation

Logsmith supports three installation modes. Choose the one that fits your project:

### Standard (recommended for most projects)

```xml
<PackageReference Include="Logsmith" Version="1.0.0" />
```

This provides the runtime library (public types, sinks, `LogManager`) and the source generator. The generator is bundled as an analyzer and does not appear in your build output.

### Standalone (zero runtime dependency)

```xml
<PackageReference Include="Logsmith.Generator" Version="1.0.0" />
```

`Logsmith.Generator` is a thin meta-package that depends on `Logsmith` for the generator and build assets only (no compile/runtime assets). It defaults to `LogsmithMode=Standalone`, and the generator emits all infrastructure types as `internal` into your assembly. No Logsmith DLLs appear in your build output.

### Abstraction (library authors)

```xml
<PropertyGroup>
    <LogsmithMode>Abstraction</LogsmithMode>
</PropertyGroup>
<PackageReference Include="Logsmith" Version="1.0.0" PrivateAssets="all" />
```

See [Operating Modes](operating-modes.md) for details on all three modes.

## Operating Modes Overview

Logsmith supports three modes, controlled by the `<LogsmithMode>` MSBuild property:

| Mode | Default for | Runtime DLL | Generated types | Use case |
|------|------------|-------------|-----------------|----------|
| **Shared** | `Logsmith` package | Yes, flows transitively | Method bodies only | Applications and multi-project solutions |
| **Standalone** | `Logsmith.Generator` package | No (`PrivateAssets="all"` required) | All types as `internal` | Libraries with zero transitive dependencies |
| **Abstraction** | Explicit opt-in | No (`PrivateAssets="all"` required) | Public interfaces + internal infrastructure | Libraries that expose logging contracts to consumers |

When both packages are referenced transitively, **Shared** wins (NuGet evaluates `Logsmith.props` before `Logsmith.Generator.props`). Explicit `<LogsmithMode>` in your `.csproj` always takes precedence.

In Standalone or Abstraction mode, the Logsmith runtime DLL must not leak to consumers. The build emits **LSMITH010** if `PrivateAssets="all"` is missing on the Logsmith package reference.

## Quick Start

### 1. Initialize at startup

```csharp
LogManager.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Debug;
    config.AddConsoleSink();
    config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
    config.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
});
```

### 2. Declare log methods

```csharp
[LogCategory("Renderer")]
public static partial class RenderLog
{
    [LogMessage(LogLevel.Debug, "Draw call {drawCallId} completed in {elapsedMs}ms")]
    public static partial void DrawCallCompleted(int drawCallId, double elapsedMs);

    [LogMessage(LogLevel.Error, "Shader compilation failed: {shaderName}")]
    public static partial void ShaderFailed(string shaderName, Exception ex);
}
```

### 3. Call them

```csharp
public class Renderer
{
    public void Draw(int id)
    {
        var sw = Stopwatch.StartNew();
        // ... rendering work ...
        sw.Stop();

        RenderLog.DrawCallCompleted(id, sw.Elapsed.TotalMilliseconds);
    }
}
```

No logger injection. No sink parameter. No service locator. The generated code dispatches through the static `LogManager` configured at startup.
