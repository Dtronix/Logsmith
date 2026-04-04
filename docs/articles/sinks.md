# Sinks

## Built-in sinks

### ConsoleSink

Writes ANSI-colored UTF8 directly to `Console.OpenStandardOutput()`, bypassing `Console.WriteLine` and its encoding overhead.

```csharp
config.AddConsoleSink();
config.AddConsoleSink(colored: false); // disable ANSI colors
```

Output format:
```
[12:34:56 DBG] Renderer: Draw call 42 completed in 1.3ms
[12:34:56 INF] Renderer: Frame rendered 100 with 50000 triangles
[12:34:56 WRN] Audio: Buffer underrun detected
[12:34:56 ERR] Network: Connection to 10.0.0.1:8080 lost
```

### FileSink

Async-buffered file writing using `Channel<T>`. The calling thread enqueues a buffered copy and returns immediately. A background task flushes to disk.

```csharp
config.AddFileSink("logs/app.log");
config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Daily);
config.AddFileSink("logs/app.log", rollingInterval: RollingInterval.Hourly, maxFileSizeBytes: 50_000_000);
config.AddFileSink("logs/app.log", shared: true); // multi-process safe
```

Rolling intervals: `None`, `Hourly`, `Daily`, `Weekly`, `Monthly`. Size-based rolling and time-based rolling can be combined. In shared mode (`shared: true`), the file is opened with `FileShare.ReadWrite` for safe concurrent appends from multiple processes.

### StreamSink

Writes to any `Stream` via async-buffered `Channel<T>`. Useful for network streams, memory streams, or `Console.OpenStandardOutput()`.

```csharp
config.AddStreamSink(networkStream, leaveOpen: true);
```

When `leaveOpen` is true, the stream is flushed but not disposed when the sink is disposed.

### DebugSink

Writes to `System.Diagnostics.Debug`, which routes to the IDE output window. Useful during development. Automatically stripped from release builds by the runtime.

```csharp
config.AddDebugSink();
```

### RecordingSink

Captures log entries to an in-memory list for test assertions. See [Testing](testing.md).

```csharp
var sink = new RecordingSink();
config.AddSink(sink);
```

### NullSink

Discards all output. Useful for benchmarking the logging pipeline itself or for disabling logging without removing call sites.

```csharp
config.AddNullSink();
```

## Sink filtering by category

Any sink can be restricted to specific categories:

```csharp
config.AddFileSink("logs/render.log", category: "Renderer");
config.AddFileSink("logs/network.log", category: "Network");
config.AddConsoleSink(); // receives everything
```

## Custom sinks

Implement `ILogSink` for text output or `IStructuredLogSink` for structured property access:

```csharp
public sealed class CustomSink : ILogSink
{
    public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // Write the formatted message to your target
    }

    public void Dispose() { }
}
```

Register at initialization:

```csharp
Log.Initialize(config =>
{
    config.AddSink(new CustomSink());
});
```

## Sink base classes

`TextLogSink` and `BufferedLogSink` provide common patterns. `BufferedLogSink` uses a `Channel<T>`-based async queue so the calling thread never blocks on I/O:

```csharp
public sealed class MyNetworkSink : BufferedLogSink
{
    protected override void Flush(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // Called on background thread, safe to do I/O
    }
}
```
