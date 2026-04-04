# Operating Modes

Logsmith supports three modes, controlled by the `<LogsmithMode>` MSBuild property:

| Mode | Default for | Runtime DLL | Generated types | Use case |
|------|------------|-------------|-----------------|----------|
| **Shared** | `Logsmith` package | Yes, flows transitively | Method bodies only | Applications and multi-project solutions |
| **Standalone** | `Logsmith.Generator` package | No (`PrivateAssets="all"` required) | All types as `internal` | Libraries with zero transitive dependencies |
| **Abstraction** | Explicit opt-in | No (`PrivateAssets="all"` required) | Public interfaces + internal infrastructure | Libraries that expose logging contracts to consumers |

When both packages are referenced transitively, **Shared** wins (NuGet evaluates `Logsmith.props` before `Logsmith.Generator.props`). Explicit `<LogsmithMode>` in your `.csproj` always takes precedence.

In Standalone or Abstraction mode, the Logsmith runtime DLL must not leak to consumers. The build emits **LSMITH010** if `PrivateAssets="all"` is missing on the Logsmith package reference.

## Abstraction Mode (Library Authors)

Abstraction mode lets library authors expose a logging interface without imposing `Logsmith.dll` on consumers. The generator emits public interfaces in a configurable namespace while keeping all infrastructure types internal.

### Setup

```xml
<PropertyGroup>
    <LogsmithMode>Abstraction</LogsmithMode>
    <!-- Optional: defaults to {RootNamespace}.Logging -->
    <LogsmithNamespace>MyLib.Logging</LogsmithNamespace>
</PropertyGroup>

<PackageReference Include="Logsmith" Version="1.0.0" PrivateAssets="all" />
```

### Generated public types

The following types are emitted as **public** in the configured namespace:

- `ILogsmithLogger` — base logging interface (text-only)
- `IStructuredLogsmithLogger` — extends `ILogsmithLogger` with typed property access via `WriteProperties<TState>`
- `LogsmithOutput` — static `Logger` property for wiring at startup
- `LogLevel`, `LogEntry`, `LogScope`, `WriteProperties<TState>`

All other infrastructure (LogManager, sinks, formatters) is emitted as `internal`.

### Library declares log methods normally

```csharp
[LogCategory("MyLib")]
static partial class LibLog
{
    [LogMessage(LogLevel.Information, "Connected to {endpoint}")]
    public static partial void Connected(string endpoint);
}
```

### Consumer wires a logger at startup

```csharp
using MyLib.Logging;

// Text-only logger
LogsmithOutput.Logger = new ConsoleLogsmithLogger();
```

```csharp
sealed class ConsoleLogsmithLogger : ILogsmithLogger
{
    public bool IsEnabled(LogLevel level, string category) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        Console.WriteLine(Encoding.UTF8.GetString(utf8Message));
    }
}
```

### Structured logger (optional)

Consumers can implement `IStructuredLogsmithLogger` to receive typed properties. Generated methods automatically detect this at runtime and dispatch to `WriteStructured` when available:

```csharp
sealed class StructuredLogger : IStructuredLogsmithLogger
{
    public bool IsEnabled(LogLevel level, string category) => true;
    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message) { /* text fallback */ }

    public void WriteStructured<TState>(in LogEntry entry, ReadOnlySpan<byte> utf8Message,
        TState state, WriteProperties<TState> propertyWriter) where TState : allows ref struct
    {
        // Use propertyWriter to write typed properties to Utf8JsonWriter
    }
}
```

## Multi-Project Solutions

### Single project or standalone application

Reference `Logsmith` or `Logsmith.Generator` alone. All types are available within the project, either as public types from the runtime library or as generated internal types.

### Multiple projects sharing log definitions

Reference `Logsmith` in every project. The runtime package includes the source generator as a bundled analyzer. All projects share the same public types (`LogLevel`, `ILogSink`, `LogEntry`, etc.) and can define their own log method classes.

```
MyApp.sln
  MyApp.Core/          --> references Logsmith (defines RenderLog, AudioLog)
  MyApp.Networking/    --> references Logsmith (defines NetworkLog)
  MyApp.Host/          --> references Logsmith (initializes LogManager, references Core + Networking)
```

The `<LogsmithMode>` property controls what the generator emits. In Shared mode (default for `Logsmith` package), it emits only the partial method bodies and uses the public types from the runtime assembly. In Standalone mode, it emits the full infrastructure as internal types.
