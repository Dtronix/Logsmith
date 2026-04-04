---
_layout: landing
---

<div style="text-align: center; padding: 0.75rem 0 0.5rem;">
  <img src="images/logo-small.png" alt="Logsmith" style="height: 64px; margin-bottom: 0.5rem;" />
  <h1 style="margin-bottom: 0.25rem; font-size: 2rem;">Logsmith</h1>
  <p style="font-size: 1.1rem; color: #666; max-width: 640px; margin: 0 auto;">
    Zero-allocation, source-generated structured logging for .NET 10.
  </p>
</div>

---

## Explore the Documentation

<div class="row" style="margin-top: 0.5rem;">
<div class="col-md-6">

### [Getting Started](articles/getting-started.md)
Install Logsmith and write your first log method in minutes.

### [Declaring Log Methods](articles/declaring-log-methods.md)
Attributes, categories, message templates, and EventId generation.

### [Log Levels](articles/log-levels.md)
Runtime filtering, per-category overrides, and compile-time level stripping.

### [Sinks](articles/sinks.md)
Six built-in sinks, category filtering, and writing custom sinks.

### [Formatting & Output](articles/formatting-and-output.md)
Log formatters, format specifiers, and structured JSON output.

### [Scoped Context](articles/scoped-context.md)
Enrich log entries with scoped properties using `LogScope.Push`.

</div>
<div class="col-md-6">

### [Advanced Features](articles/advanced-features.md)
Sampling, rate limiting, dynamic level switching, and global exception handling.

### [MEL Bridge](articles/mel-bridge.md)
Route `ILogger` calls through Logsmith sinks with the Extensions.Logging bridge.

### [Operating Modes](articles/operating-modes.md)
Shared, Standalone, and Abstraction modes for apps and libraries.

### [Performance](articles/performance.md)
`in` parameters, custom serialization, caller info, and benchmarks.

### [Testing](articles/testing.md)
Capture and assert log output with `RecordingSink`.

### [Configuration Reference](articles/configuration-reference.md)
LogManager API, MSBuild properties, flushing, and shutdown.

</div>
</div>

---

## Quick Start

```csharp
// 1. Initialize at startup
LogManager.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Debug;
    config.AddConsoleSink();
    config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
});

// 2. Declare log methods
[LogCategory("Renderer")]
public static partial class RenderLog
{
    [LogMessage(LogLevel.Debug, "Draw call {drawCallId} completed in {elapsedMs}ms")]
    public static partial void DrawCallCompleted(int drawCallId, double elapsedMs);

    [LogMessage(LogLevel.Error, "Shader compilation failed: {shaderName}")]
    public static partial void ShaderFailed(string shaderName, Exception ex);
}

// 3. Call them
RenderLog.DrawCallCompleted(42, 1.23);
```

The generator emits fully specialized, zero-allocation UTF8 code for each log method at compile time. See [Getting Started](articles/getting-started.md) for the full walkthrough.

---

## Why Logsmith?

<div class="row" style="margin-top: 1rem;">
<div class="col-md-4" style="margin-bottom: 1.5rem;">
<h3>Zero-Allocation Pipeline</h3>
<p>Every log call writes directly to sinks with no heap allocation, no boxing, and no string interpolation. Stackalloc buffers and UTF8 literals throughout.</p>
</div>
<div class="col-md-4" style="margin-bottom: 1.5rem;">
<h3>Source-Generated</h3>
<p>A Roslyn source generator emits fully specialized code per log method at compile time. No reflection, no runtime parsing, fully NativeAOT compatible.</p>
</div>
<div class="col-md-4" style="margin-bottom: 1.5rem;">
<h3>Three Operating Modes</h3>
<p>Shared (runtime library), Standalone (zero-dependency), or Abstraction (library authors). Pick what fits your deployment.</p>
</div>
</div>

<div class="row">
<div class="col-md-4" style="margin-bottom: 1.5rem;">
<h3>Structured JSON Output</h3>
<p><code>IStructuredLogSink</code> receives typed properties via <code>Utf8JsonWriter</code>. No runtime serialization overhead — the generator writes each field directly.</p>
</div>
<div class="col-md-4" style="margin-bottom: 1.5rem;">
<h3>Compile-Time Level Stripping</h3>
<p><code>[Conditional]</code> attributes remove log calls below a configured threshold at compile time. Stripped levels have zero cost — the calls don't exist in the binary.</p>
</div>
<div class="col-md-4" style="margin-bottom: 1.5rem;">
<h3>MEL Bridge</h3>
<p><code>Logsmith.Extensions.Logging</code> routes <code>ILogger</code> calls through Logsmith sinks. Drop-in integration for existing dependency injection setups.</p>
</div>
</div>
