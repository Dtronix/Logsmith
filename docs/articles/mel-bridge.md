# Microsoft.Extensions.Logging Bridge

The `Logsmith.Extensions.Logging` package routes `ILogger` calls through Logsmith sinks. This enables Logsmith as the backend for libraries and frameworks that log through MEL.

## Installation

```xml
<PackageReference Include="Logsmith" Version="1.0.0" />
<PackageReference Include="Logsmith.Extensions.Logging" Version="1.0.0" />
```

## Registration

```csharp
using Logsmith.Extensions.Logging;

// Initialize Logsmith sinks
LogManager.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Debug;
    config.AddConsoleSink();
});

// Register as MEL provider
var services = new ServiceCollection();
services.AddLogging(builder =>
{
    builder.AddLogsmith();
});
```

## Level mapping

MEL log levels map 1:1 to Logsmith levels — both enums use Trace=0 through Critical=5, None=6.

## Scope integration

MEL's `ILogger.BeginScope` delegates to `LogScope.Push`. Scopes are shared between MEL and direct Logsmith usage:

```csharp
// MEL scope
using (melLogger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = "corr-xyz" }))
{
    melLogger.LogInformation("From MEL");
    // Direct Logsmith call also sees the scope
    Log.ProcessingOrder("ORD-001", "Alice");
    // Both outputs include [CorrelationId=corr-xyz]
}
```

String-typed `BeginScope` states are stored as `[Scope=value]`.

## Category handling

`ILoggerFactory.CreateLogger(categoryName)` maps the category directly to the Logsmith `LogEntry.Category` field. Per-category minimum levels configured via `SetMinimumLevel` apply to MEL loggers.
