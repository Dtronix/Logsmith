# Log Levels

## Log levels

```csharp
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

## Runtime filtering

`LogManager` performs an enum comparison before dispatching. If the entry's level is below the configured minimum, no work is done:

```csharp
// Fast path: single enum comparison, no allocations
if (level < _config.MinimumLevel) return;
```

Per-category overrides are supported. You can use a magic string or the type-safe generic overload, which reads the `CategoryName` constant emitted by the generator on each log class:

```csharp
LogManager.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Information;
    config.SetMinimumLevel("Renderer", LogLevel.Debug);         // by string
    config.SetMinimumLevel<RenderLog>(LogLevel.Debug);          // by type (recommended)
});
```

The generic overload resolves the category from the generated `public const string CategoryName` field. If the type has a `[LogCategory]` attribute, the constant reflects that name; otherwise it defaults to the class name.

## Compile-time stripping

The generator applies `[Conditional("DEBUG")]` to log methods at or below a configurable severity threshold. The C# compiler erases these call sites entirely from release builds. No IL is emitted. Arguments are not evaluated.

Configure the threshold in your project file:

```xml
<PropertyGroup>
    <!-- Default: Debug. Methods at Trace and Debug get [Conditional("DEBUG")] -->
    <LogsmithConditionalLevel>Debug</LogsmithConditionalLevel>
</PropertyGroup>
```

| Setting | Methods stripped in Release |
|---|---|
| `Trace` | Trace only |
| `Debug` (default) | Trace, Debug |
| `Information` | Trace, Debug, Information |
| `None` | Nothing stripped |

To exempt a specific method from stripping regardless of the threshold:

```csharp
[LogMessage(LogLevel.Debug, "Critical diagnostic: {value}", AlwaysEmit = true)]
public static partial void CriticalDiagnostic(double value);
```
