# Scoped Context

`LogScope` provides ambient key-value properties that attach to every log entry within a scope. Scopes use `AsyncLocal<T>` and propagate through `async`/`await` boundaries.

## Basic usage

```csharp
using (LogScope.Push("RequestId", "req-abc-123"))
{
    Log.ProcessingOrder("ORD-001", "Alice");
    // Text output: "Processing order ORD-001 for Alice [RequestId=req-abc-123]"
}
// Outside the scope — no enrichment
Log.ProcessingOrder("ORD-002", "Bob");
// Text output: "Processing order ORD-002 for Bob"
```

## Nested scopes

Scopes nest naturally. Inner properties appear alongside outer properties:

```csharp
using (LogScope.Push("RequestId", "req-abc-123"))
{
    using (LogScope.Push("UserId", "user-42"))
    {
        Log.ProcessingOrder("ORD-001", "Alice");
        // Text output: "Processing order ORD-001 for Alice [UserId=user-42] [RequestId=req-abc-123]"
    }
}
```

## Multi-property push

Push multiple properties in a single call:

```csharp
var props = new KeyValuePair<string, string>[]
{
    new("RequestId", "req-abc-123"),
    new("TenantId", "tenant-7"),
};
using (LogScope.Push(props))
{
    Log.SomeMethod();
}
```

## Structured sinks

Scope properties are available to structured sinks via `LogScope.WriteToJson(Utf8JsonWriter)`, which writes each property as a JSON string property.

## Performance

Scope enrichment has zero overhead on the dispatch hot path when no scopes are active. When scopes are active, a 512-byte `stackalloc` buffer is used to build the enriched message — no heap allocation.
