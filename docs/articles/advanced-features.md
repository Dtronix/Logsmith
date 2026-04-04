# Advanced Features

## Log Sampling and Rate Limiting

High-frequency log methods can be throttled at compile time using attributes on `[LogMessage]`. The generator emits lightweight guards that execute before any formatting or dispatch work.

### Sampling

`SampleRate = N` emits every Nth log call. Uses a single `Interlocked.Increment` and modulo check:

```csharp
// Only 1 in 10 heartbeat messages will be emitted
[LogMessage(LogLevel.Debug, "Heartbeat", SampleRate = 10)]
public static partial void Heartbeat();
```

The counter wraps naturally on `int` overflow. No lock, no allocation.

### Rate limiting

`MaxPerSecond = N` caps throughput to N messages per second using a per-second time window:

```csharp
// At most 100 messages per second
[LogMessage(LogLevel.Warning, "Request throttled", MaxPerSecond = 100)]
public static partial void RequestThrottled();
```

Window reset is a benign race — a few extra messages may slip through at the boundary. This is a logging rate limiter, not a security rate limiter.

### Combining both

When both are set, `SampleRate` is applied first:

```csharp
[LogMessage(LogLevel.Debug, "Tick", SampleRate = 5, MaxPerSecond = 50)]
public static partial void Tick();
```

The generator emits `LSMITH007` as a warning when both are set on the same method.

### Generated code

No guards are emitted when `SampleRate` is 0 or 1 and `MaxPerSecond` is 0. When active, the generator emits static counter fields and guard code at the top of the method body, after the `IsEnabled` check.

## Dynamic Level Switching

Two opt-in mechanisms for adjusting log levels at runtime without calling `Reconfigure`.

### Environment variable polling

```csharp
LogManager.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Debug;
    config.AddConsoleSink();
    config.WatchEnvironmentVariable("MY_LOG_LEVEL", pollInterval: TimeSpan.FromSeconds(5));
});
```

The monitor reads the environment variable on each poll tick and calls `Enum.TryParse<LogLevel>`. If the value changed, the minimum level is updated lock-free. Default poll interval is 5 seconds.

### Config file watching

```csharp
LogManager.Initialize(config =>
{
    config.MinimumLevel = LogLevel.Debug;
    config.AddConsoleSink();
    config.WatchConfigFile("logsmith.json");
});
```

The monitor uses `FileSystemWatcher` with a 500ms debounce. The JSON format:

```json
{
    "MinimumLevel": "Warning",
    "CategoryOverrides": {
        "Noisy": "Error",
        "Network": "Debug"
    }
}
```

Both `MinimumLevel` and `CategoryOverrides` are optional. Parse errors are silently ignored (the file may be partially written).

### Lifecycle

Monitors are created during `Build()` and stored in the config. When `Reconfigure` replaces the config, old monitors are disposed. `Reset()` also disposes monitors for test isolation.

## Global Exception Handler

Explicit opt-in for capturing unhandled and unobserved task exceptions, configured via the builder.

```csharp
LogManager.Initialize(cfg =>
{
    cfg.AddConsoleSink();
    cfg.InternalErrorHandler = ex => Console.Error.WriteLine(ex);
    cfg.CaptureUnhandledExceptions();
});
```

This wires:
- `AppDomain.CurrentDomain.UnhandledException` — captures unhandled exceptions on any thread.
- `TaskScheduler.UnobservedTaskException` — captures exceptions from unawaited tasks.

Captured exceptions are routed to `InternalErrorHandler`. The handler runs inside a `try/catch` — a failing handler cannot crash the process.

### Observing task exceptions

By default, unobserved task exceptions are logged but not observed. To also call `SetObserved()` (preventing process termination in certain configurations):

```csharp
cfg.CaptureUnhandledExceptions(observeTaskExceptions: true);
```

### Lifecycle

Exception capture is tied to the logging configuration lifecycle:
- `Reconfigure()` — old config unsubscribes, new config subscribes (if configured).
- `Shutdown()` — unsubscribes automatically.

No manual `StopCapturing` call is needed.
