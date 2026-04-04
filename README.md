# <img src="./docs/images/logo-small.png" height="48"> Logsmith [![CI](https://github.com/Dtronix/Logsmith/actions/workflows/ci.yml/badge.svg)](https://github.com/Dtronix/Logsmith/actions/workflows/ci.yml)

Zero-allocation, source-generated structured logging for .NET 10.

Logsmith is a logging framework where the source generator *is* the framework. Every log method is analyzed at compile time, and the generator emits fully specialized, zero-allocation UTF8 code tailored to your exact parameters. No reflection. No boxing. No runtime parsing of message templates.

**[Documentation](https://dtronix.github.io/Logsmith/)** | **[API Reference](https://dtronix.github.io/Logsmith/api/)**

---

## Packages

| Name | NuGet | Description |
|------|-------|-------------|
| [`Logsmith`](https://www.nuget.org/packages/Logsmith) | [![Logsmith](https://img.shields.io/nuget/v/Logsmith.svg?maxAge=60)](https://www.nuget.org/packages/Logsmith) | Runtime library with public types, sinks, and the bundled source generator. Default mode: **Shared**. |
| [`Logsmith.Generator`](https://www.nuget.org/packages/Logsmith.Generator) | [![Logsmith.Generator](https://img.shields.io/nuget/v/Logsmith.Generator.svg?maxAge=60)](https://www.nuget.org/packages/Logsmith.Generator) | Thin meta-package. Depends on `Logsmith` for the generator and build assets only — no runtime DLL. Default mode: **Standalone**. |
| [`Logsmith.Extensions.Logging`](https://www.nuget.org/packages/Logsmith.Extensions.Logging) | [![Logsmith.Extensions.Logging](https://img.shields.io/nuget/v/Logsmith.Extensions.Logging.svg?maxAge=60)](https://www.nuget.org/packages/Logsmith.Extensions.Logging) | Microsoft.Extensions.Logging bridge. Routes `ILogger` calls through Logsmith sinks. |

---

## Why Logsmith

Most .NET logging frameworks parse message templates at runtime, box value-type arguments into `object[]`, and dispatch through multiple abstraction layers before bytes reach an output target. For applications where logging sits on the hot path — game engines, trading systems, NativeAOT deployments — those costs are measurable.

Logsmith takes the source-generator approach to its conclusion: the generator does not supplement a runtime framework, it replaces it entirely. It reads your method declarations at build time, knows the concrete types of every parameter, and emits direct UTF8 formatting calls with pre-computed property names and type-specific serialization paths.

---

## Comparison with Other Frameworks

| Capability | Logsmith | MEL + LoggerMessage | ZLogger | Serilog | NLog |
|---|---|---|---|---|---|
| Source-generated method bodies | Yes | Yes | Yes | No | No |
| Zero runtime dependency mode | Yes (standalone) | No (requires MEL) | No (requires MEL) | No | No |
| Abstraction mode for libraries | Yes | No | No | No | No |
| Zero allocation hot path | Yes | Partial (MEL infra allocates) | Yes | No | No |
| UTF8 end-to-end | Yes | No (UTF16 strings) | Yes | No | No |
| Structured logging | Yes (Utf8JsonWriter) | Yes | Yes | Yes | Yes |
| Compile-time level stripping | Yes ([Conditional]) | No | No | No | No |
| No boxing of value types | Yes | Yes (generated path) | Yes | No (object[] params) | No (object[] params) |
| No reflection | Yes | Yes | Partial | No (used in enrichers) | No (used in layouts) |
| NativeAOT compatible | Yes | Yes | Yes | Partial | Partial |
| Compile-time diagnostics | Yes (LSMITH001-010) | Yes | Limited | No | No |
| Log sampling / rate limiting | Yes (compile-time) | No | No | No | No |
| Scoped context (AsyncLocal) | Yes (LogScope) | Yes (ILogger.BeginScope) | Yes | Yes (LogContext) | Yes (ScopeContext) |
| Dynamic level switching | Yes (env var, file) | Yes (IOptionsMonitor) | Yes | Yes (LoggingLevelSwitch) | Yes (config reload) |
| Custom type serialization | ILogStructurable | ILogger.BeginScope | IZLoggerFormattable | Destructure policies | Custom layout renderers |
| MEL ecosystem compatibility | Yes (bridge package) | Native | Native | Via Serilog.Extensions.Logging | Via NLog.Extensions.Logging |
| DI container required | No | Typically yes | Typically yes | No | No |
| Transitive dependencies | Zero (standalone) | MEL abstractions | MEL + ZLogger | Serilog + sinks | NLog |

---

## Features

- **[Zero-allocation logging pipeline](https://dtronix.github.io/Logsmith/articles/sinks.html)** — every log call writes directly to sinks with no heap allocation, no boxing, no string interpolation
- **[Source-generated method bodies](https://dtronix.github.io/Logsmith/articles/declaring-log-methods.html)** — the generator emits fully specialized UTF8 code per log method at compile time
- **[Compile-time level stripping](https://dtronix.github.io/Logsmith/articles/log-levels.html)** — `[Conditional]` attributes remove log calls below a configured threshold from release builds
- **[Three operating modes](https://dtronix.github.io/Logsmith/articles/operating-modes.html)** — Shared, Standalone, or Abstraction for apps and libraries
- **[Six built-in sinks](https://dtronix.github.io/Logsmith/articles/sinks.html)** — Console, File, Stream, Debug, Recording, and Null with category filtering
- **[Structured JSON output](https://dtronix.github.io/Logsmith/articles/formatting-and-output.html)** — `IStructuredLogSink` receives typed properties via `Utf8JsonWriter`
- **[Scoped context enrichment](https://dtronix.github.io/Logsmith/articles/scoped-context.html)** — attach ambient properties via `LogScope.Push` with zero overhead when inactive
- **[Log sampling and rate limiting](https://dtronix.github.io/Logsmith/articles/advanced-features.html)** — throttle high-frequency log methods with compile-time guards
- **[Dynamic level switching](https://dtronix.github.io/Logsmith/articles/advanced-features.html)** — adjust log levels at runtime via environment variables or config files
- **[MEL bridge](https://dtronix.github.io/Logsmith/articles/mel-bridge.html)** — route `ILogger` calls through Logsmith sinks with `Logsmith.Extensions.Logging`
- **[Custom type serialization](https://dtronix.github.io/Logsmith/articles/performance.html)** — implement `IUtf8SpanFormattable` and `ILogStructurable` for optimal formatting
- **[Compile-time diagnostics](https://dtronix.github.io/Logsmith/internals/diagnostics.html)** — template mismatches, missing parameters, and unsupported types caught before runtime

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

The generator emits fully specialized, zero-allocation UTF8 code for each log method at compile time. See [Getting Started](https://dtronix.github.io/Logsmith/articles/getting-started.html) for the full walkthrough.

---

## Benchmarks

Full benchmark results comparing Logsmith against MEL, Serilog, NLog, and ZLogger are available in [docs/benchmarks.md](https://dtronix.github.io/Logsmith/articles/performance.html).

---

## License

This project is licensed under the [MIT License](LICENSE).
