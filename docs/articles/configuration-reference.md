# Configuration Reference

## LogManager initialization

```csharp
Log.Initialize(config =>
{
    // Global minimum level (default: Information)
    config.MinimumLevel = LogLevel.Debug;

    // Per-category minimum level override
    config.SetMinimumLevel("Renderer", LogLevel.Trace);         // by string
    config.SetMinimumLevel<NetworkLog>(LogLevel.Warning);       // by type (recommended)

    // Internal error handler for sink exceptions
    config.InternalErrorHandler = ex => Console.Error.WriteLine($"Logging error: {ex}");

    // Add sinks (all accept optional ILogFormatter parameter)
    config.AddConsoleSink();
    config.AddConsoleSink(colored: false, formatter: NullLogFormatter.Instance);
    config.AddFileSink("logs/app.log");
    config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
    config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Hourly,
                       maxFileSizeBytes: 50_000_000);
    config.AddFileSink("logs/app.log", shared: true);  // multi-process safe
    config.AddStreamSink(networkStream, leaveOpen: true);
    config.AddDebugSink();
    config.AddSink(new CustomSink());

    // Dynamic level switching (optional)
    config.WatchEnvironmentVariable("MY_LOG_LEVEL");
    config.WatchEnvironmentVariable("MY_LOG_LEVEL", pollInterval: TimeSpan.FromSeconds(10));
    config.WatchConfigFile("logsmith.json");
});
```

## Runtime reconfiguration

```csharp
LogManager.Reconfigure(config =>
{
    config.ClearSinks();
    config.MinimumLevel = LogLevel.Warning;
    config.AddConsoleSink();
});
```

The configuration object is immutable. Reconfiguration builds a new config and swaps it atomically via a volatile write. The hot path reads the config through a single volatile read with no locking. Active monitors from the previous config are disposed.

## Flushing and shutdown

Sinks that implement `IFlushableLogSink` (including `FileSink`, `StreamSink`, and `BufferedLogSink` subclasses) support explicit flushing:

```csharp
// Flush all buffered sinks
await LogManager.FlushAsync();
await LogManager.FlushAsync(timeout: TimeSpan.FromSeconds(5));

// Graceful shutdown: flush all sinks, then dispose
await LogManager.ShutdownAsync();
await LogManager.ShutdownAsync(timeout: TimeSpan.FromSeconds(10));

// Synchronous shutdown (blocks)
LogManager.Shutdown();
```

`BufferedLogSink` tracks dropped messages when its bounded channel is full. The `DroppedCount` property reports the total, and drops are reported via the `errorHandler` callback (as `LogDroppedException`).

## Global exception handler

```csharp
LogManager.Initialize(cfg =>
{
    cfg.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
    cfg.CaptureUnhandledExceptions(observeTaskExceptions: false); // true to call SetObserved()
    // ...
});
// Automatically unsubscribed on Reconfigure() or Shutdown()
```

## MSBuild properties

```xml
<PropertyGroup>
    <!-- Operating mode: Shared, Standalone, or Abstraction -->
    <!-- Default: Shared (Logsmith package) or Standalone (Logsmith.Generator package) -->
    <LogsmithMode>Shared</LogsmithMode>

    <!-- Conditional compilation threshold (default: Debug) -->
    <LogsmithConditionalLevel>Debug</LogsmithConditionalLevel>

    <!-- Namespace for abstraction mode public types (default: {RootNamespace}.Logging) -->
    <LogsmithNamespace>MyLib.Logging</LogsmithNamespace>
</PropertyGroup>
```
