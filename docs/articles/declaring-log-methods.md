# Declaring Log Methods

Log methods are declared as `static partial` methods inside `partial` classes. The generator provides the implementation.

```csharp
public static partial class NetworkLog
{
    [LogMessage(LogLevel.Information, "Connection established to {endpoint} in {latencyMs}ms")]
    public static partial void ConnectionEstablished(string endpoint, double latencyMs);

    [LogMessage(LogLevel.Warning, "Packet loss detected: {lossPercent}% over {windowSeconds}s")]
    public static partial void PacketLoss(float lossPercent, int windowSeconds);

    [LogMessage(LogLevel.Critical, "Connection to {endpoint} lost")]
    public static partial void ConnectionLost(string endpoint, Exception ex);
}
```

Requirements:
- The containing class must be `partial`.
- The method must be `static partial`.
- The method must return `void`.
- Parameter names referenced in the message template are matched case-insensitively.
- Parameters may use the `in` modifier to pass large structs by reference (see [Performance](performance.md)).

## Categories

The `[LogCategory]` attribute sets the category string attached to every log entry from that class. If omitted, the class name is used. The generator emits a `public const string CategoryName` field on each log class, which can be used for type-safe per-category configuration via `SetMinimumLevel<T>()`.

```csharp
[LogCategory("Audio")]
public static partial class AudioLog { ... }
// Generated: public const string CategoryName = "Audio";

// No attribute: category defaults to "PhysicsLog"
public static partial class PhysicsLog { ... }
// Generated: public const string CategoryName = "PhysicsLog";
```

## Message Templates

### Explicit templates

Provide a message string with `{parameterName}` placeholders that map to method parameters by name (case-insensitive):

```csharp
[LogMessage(LogLevel.Debug, "Frame {frameId} rendered {triangleCount} triangles in {elapsedMs}ms")]
public static partial void FrameRendered(int frameId, long triangleCount, double elapsedMs);
```

The generator pre-splits the template at compile time into alternating literal segments and parameter slots. At runtime, it writes UTF8 literals directly and formats each parameter through its `IUtf8SpanFormattable` implementation.

### Template-free mode

Omit the message string. The generator constructs the message automatically from the method name and parameter names:

```csharp
[LogMessage(LogLevel.Debug)]
public static partial void FrameRendered(int frameId, long triangleCount, double elapsedMs);
// Generated message: "FrameRendered frameId={frameId} triangleCount={triangleCount} elapsedMs={elapsedMs}"
```

This mode guarantees that renaming a parameter via IDE refactoring keeps the message in sync. The method name is split from PascalCase for the structured event name.

### EventId

Each log method receives a stable `EventId` derived from a hash of the fully qualified method name. To override:

```csharp
[LogMessage(LogLevel.Information, "Player joined: {playerName}", EventId = 5001)]
public static partial void PlayerJoined(string playerName);
```
