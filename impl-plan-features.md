# Implementation Plan — Logsmith Feature Batch

Seven features. Ordered by dependency chain: foundational changes first, features that depend on them after.

## Dependency Order

1. **Thread Info Capture** — modifies `LogEntry` (many features touch LogEntry)
2. **Internal Error Handler** — modifies `LogManager.Dispatch` and `LogConfig` (must be in place before new sinks)
3. **Format Specifiers in Templates** — modifies generator pipeline (`TemplatePart`, `TemplateParser`, `TextPathEmitter`, `StructuredPathEmitter`)
4. **ILogFormatter (Prefix/Suffix)** — new abstraction, modifies `LogConfigBuilder`, all existing sinks
5. **Stream Sink** — new sink, depends on ILogFormatter
6. **Multi-Process File Coordination** — modifies `FileSink`
7. **Time-Interval Rolling** — modifies `FileSink`, depends on multi-process changes

---

## 1. Thread Info Capture

**Goal**: Every `LogEntry` carries `ThreadId` (int) and `ThreadName` (string?) captured at call site.

### 1.1 Modify `LogEntry`

File: `src/Logsmith/LogEntry.cs`

Add two fields after `CallerMember`:

```csharp
public readonly int ThreadId;
public readonly string? ThreadName;
```

Extend constructor with two new trailing parameters (defaulted for backward compat):

```csharp
public LogEntry(..., int threadId = 0, string? threadName = null)
```

`Environment.CurrentManagedThreadId` is a direct field read — effectively free. `Thread.CurrentThread.Name` is a property read on a cached string — negligible cost.

### 1.2 Modify Generator — `MethodEmitter.EmitLogEntryConstruction`

File: `src/Logsmith.Generator/Emission/MethodEmitter.cs`

Append to the `new LogEntry(...)` construction:

```csharp
sb.AppendLine($"            threadId: global::System.Environment.CurrentManagedThreadId,");
sb.AppendLine($"            threadName: global::System.Threading.Thread.CurrentThread.Name);");
```

No new parameters on user methods. Captured automatically inside the generated body.

### 1.3 Modify `BufferedLogSink.BufferedEntry`

File: `src/Logsmith/Sinks/BufferedLogSink.cs`

Add `int ThreadId, string? ThreadName` fields to the `BufferedEntry` record struct. Update `Write(in LogEntry ...)` to copy these fields from the entry.

### 1.4 Propagate to Sinks (Display)

Thread info display is deferred to Feature 4 (ILogFormatter). The default formatter will include `[T{ThreadId}]` in its prefix. Sinks that format their own prefix (ConsoleSink, FileSink) will use the formatter after Feature 4 lands. Until then, the data is captured and available but not rendered.

### 1.5 Update `RecordingSink.CapturedEntry`

File: `src/Logsmith/Sinks/RecordingSink.cs`

Add `ThreadId` and `ThreadName` to `CapturedEntry` record. Update `Write` to capture from `LogEntry`.

### 1.6 Tests

File: `tests/Logsmith.Tests/LogEntryTests.cs` (new)

| Test | Asserts |
|------|---------|
| `ThreadId_CapturedAtConstruction` | `entry.ThreadId == Environment.CurrentManagedThreadId` |
| `ThreadName_CapturedAtConstruction` | `entry.ThreadName == Thread.CurrentThread.Name` |
| `ThreadName_NullWhenUnnamed` | Set thread name null → `entry.ThreadName` is null |

File: `tests/Logsmith.Tests/SinkTests/RecordingSinkTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `CapturedEntry_IncludesThreadInfo` | `sink.Entries[0].ThreadId > 0`, `ThreadName` matches |

File: `tests/Logsmith.Generator.Tests/CodeEmissionTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `ThreadInfo_EmittedInLogEntry` | Generated source contains `threadId: global::System.Environment.CurrentManagedThreadId` |

---

## 2. Internal Error Handler

**Goal**: Sink exceptions in `LogManager.Dispatch` are caught and routed to a configurable `Action<Exception>` instead of propagating to the caller.

### 2.1 Add Error Handler to Config

File: `src/Logsmith/LogConfigBuilder.cs`

```csharp
public Action<Exception>? InternalErrorHandler { get; set; }
```

File: `src/Logsmith/Internal/LogConfig.cs`

```csharp
internal readonly Action<Exception>? ErrorHandler;
```

Pass through from builder in `Build()`.

### 2.2 Wrap Sink Calls in `LogManager.Dispatch`

File: `src/Logsmith/LogManager.cs`

Wrap each `sink.Write` / `sink.WriteStructured` call in try/catch:

```csharp
try
{
    textSinks[i].Write(in entry, utf8Message);
}
catch (Exception ex)
{
    config.ErrorHandler?.Invoke(ex);
}
```

Same pattern for structured sinks. If `ErrorHandler` is null, exceptions are silently swallowed (logging must never crash the app). A failed sink does not prevent other sinks from executing.

### 2.3 Tests

File: `tests/Logsmith.Tests/LogManagerTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `Dispatch_SinkThrows_CallsErrorHandler` | Add throwing sink + error handler → handler receives the exception |
| `Dispatch_SinkThrows_OtherSinksStillRun` | Throwing sink first, RecordingSink second → RecordingSink still gets entry |
| `Dispatch_SinkThrows_NoHandler_DoesNotPropagate` | No error handler configured → no exception escapes Dispatch |
| `Dispatch_ErrorHandler_ReceivesOriginalException` | Verify `ex.Message` matches the thrown exception |

Helper: `ThrowingSink : ILogSink` — `IsEnabled` returns true, `Write` throws `InvalidOperationException`.

---

## 3. Format Specifiers in Templates

**Goal**: Support `{value:F2}` (standard .NET format strings) and `{obj:json}` (JSON serialization) in message templates.

### 3.1 Extend `TemplatePart` Model

File: `src/Logsmith.Generator/Models/TemplatePart.cs`

Add property:

```csharp
public string? FormatSpecifier { get; internal set; }
```

Set during parsing. `null` means no specifier (current behavior). `:json` is stored as the literal string `"json"`. Standard format strings stored as-is (e.g., `"F2"`, `"yyyy-MM-dd"`).

### 3.2 Modify `TemplateParser.Parse`

File: `src/Logsmith.Generator/Parsing/TemplateParser.cs`

Inside placeholder extraction, after finding `{...}` content, split on first `:`:

```
{name}       → placeholder="name",  format=null
{value:F2}   → placeholder="value", format="F2"
{obj:json}   → placeholder="obj",   format="json"
```

Algorithm: `int colonIdx = placeholderContent.IndexOf(':')`. If found, `name = content[..colonIdx]`, `format = content[(colonIdx+1)..]`. Store format on the `TemplatePart`.

Bind step unchanged — binding matches on `part.Text` (the name portion only).

### 3.3 Modify `Utf8LogWriter` — Add Format Overload

File: `src/Logsmith/Utf8LogWriter.cs`

New method:

```csharp
public void WriteFormatted<T>(in T value, ReadOnlySpan<char> format) where T : IUtf8SpanFormattable
```

Calls `value.TryFormat(_buffer[_position..], out int bytesWritten, format, null)`.

### 3.4 Modify `TextPathEmitter` — Emit Format Specifier

File: `src/Logsmith.Generator/Emission/TextPathEmitter.cs`

In `EmitParameterWrite`, check `part.FormatSpecifier`:

- **null**: Current behavior (`writer.WriteFormatted(value)` or `writer.WriteString(value)`).
- **"json"**: Emit JSON serialization path (see 3.5).
- **other**: Emit `writer.WriteFormatted(value, "{format}")` passing format string as `ReadOnlySpan<char>`.

For strings with a non-json format specifier: emit `writer.WriteString(value)` (strings don't support format specifiers — ignore silently, or emit LSMITH diagnostic).

### 3.5 JSON Format Specifier — `:json`

When `FormatSpecifier == "json"`, the text path emits:

```csharp
// Generated code for {obj:json}:
var __jsonBytes = global::System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(obj);
writer.Write(__jsonBytes);
```

This uses `JsonSerializer.SerializeToUtf8Bytes` which returns `byte[]`. The `Utf8LogWriter.Write(ReadOnlySpan<byte>)` method already exists.

**Allocation note**: `:json` allocates (`byte[]` from serializer). This is acceptable — it's opt-in for complex objects. Document that `:json` is not zero-alloc.

For the structured path, `:json` writes the object as a raw JSON value using `JsonSerializer.Serialize(writer, value)` where writer is the `Utf8JsonWriter`. See 3.6.

### 3.6 Modify `StructuredPathEmitter` — Format-Aware Property Writes

File: `src/Logsmith.Generator/Emission/StructuredPathEmitter.cs`

When `FormatSpecifier == "json"`:

```csharp
writer.WritePropertyName("paramName");
global::System.Text.Json.JsonSerializer.Serialize(writer, state.paramName);
```

When format specifier is a standard .NET format string: apply `.ToString(format)` instead of plain `.ToString()`:

```csharp
writer.WriteString("paramName", state.paramName.ToString("F2"));
```

When format specifier is null: unchanged behavior.

### 3.7 Buffer Size Estimation

File: `src/Logsmith.Generator/Emission/MethodEmitter.cs`

`:json` params get +256 estimate (JSON objects are larger). Standard format specifiers don't change the estimate.

### 3.8 New Diagnostic — LSMITH006

File: `src/Logsmith.Generator/Diagnostics/DiagnosticDescriptors.cs`

```csharp
internal static readonly DiagnosticDescriptor LSMITH006; // ":json specifier used on primitive type — unnecessary, prefer default formatting"
```

Severity: Warning. Emitted when `:json` is applied to `string`, `int`, `bool`, etc.

### 3.9 Tests

File: `tests/Logsmith.Generator.Tests/TemplateParsingTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `Parse_FormatSpecifier_ExtractedFromPlaceholder` | `{value:F2}` → part.Text="value", part.FormatSpecifier="F2" |
| `Parse_JsonSpecifier_Recognized` | `{obj:json}` → part.FormatSpecifier="json" |
| `Parse_NoSpecifier_FormatIsNull` | `{name}` → part.FormatSpecifier is null |
| `Parse_ColonInLiteral_NotTreatedAsSpecifier` | `"time: {val}"` → literal "time: " + placeholder "val" |

File: `tests/Logsmith.Generator.Tests/CodeEmissionTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `FormatSpecifier_EmitsWriteFormattedWithFormat` | `{count:N0}` → generated contains `WriteFormatted(count, "N0")` |
| `JsonSpecifier_EmitsSerializeToUtf8Bytes` | `{data:json}` → generated contains `SerializeToUtf8Bytes` |
| `JsonSpecifier_StructuredPath_EmitsSerialize` | `{data:json}` → WriteProperties method contains `JsonSerializer.Serialize(writer` |

File: `tests/Logsmith.Tests/Utf8LogWriterTests.cs` (new)

| Test | Asserts |
|------|---------|
| `WriteFormatted_WithFormat_AppliesFormatString` | `WriteFormatted(3.14159, "F2")` → buffer contains "3.14" |
| `WriteFormatted_WithEmptyFormat_SameAsDefault` | `WriteFormatted(42, "")` → buffer contains "42" |

File: `tests/Logsmith.Generator.Tests/DiagnosticTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `LSMITH006_JsonOnPrimitive_EmitsWarning` | `{count:json}` with int param → LSMITH006 warning |

---

## 4. ILogFormatter (Configurable Prefix/Suffix)

**Goal**: Pluggable log line formatting via `ILogFormatter` interface using `IBufferWriter<byte>` for zero-allocation output.

### 4.1 Define `ILogFormatter`

File: `src/Logsmith/Formatting/ILogFormatter.cs` (new)

```csharp
namespace Logsmith.Formatting;

public interface ILogFormatter
{
    void FormatPrefix(in LogEntry entry, IBufferWriter<byte> output);
    void FormatSuffix(in LogEntry entry, IBufferWriter<byte> output);
}
```

Receives the full `LogEntry` (including new `ThreadId`/`ThreadName` from Feature 1). Writes UTF-8 bytes directly to the `IBufferWriter<byte>`.

### 4.2 `DefaultLogFormatter`

File: `src/Logsmith/Formatting/DefaultLogFormatter.cs` (new)

Replicates current ConsoleSink/FileSink prefix behavior. Constructor options:

```csharp
public sealed class DefaultLogFormatter : ILogFormatter
{
    public DefaultLogFormatter(bool includeDate = false, bool includeThread = false);
    public void FormatPrefix(in LogEntry entry, IBufferWriter<byte> output);
    public void FormatSuffix(in LogEntry entry, IBufferWriter<byte> output);
}
```

**FormatPrefix algorithm**: Write directly to `IBufferWriter<byte>` using `Span<byte>` from `output.GetSpan()` / `output.Advance()`.

- `includeDate=false`: `[HH:mm:ss.fff LVL Category] ` (console style)
- `includeDate=true`: `[yyyy-MM-dd HH:mm:ss.fff LVL Category] ` (file style)
- `includeThread=true`: inserts `T{ThreadId}` after level tag: `[HH:mm:ss.fff LVL T12 Category] `

Timestamp formatted via `DateTime` → `TryFormat` into the span. Level tag via lookup table of `ReadOnlySpan<byte>` constants. Category via `Utf8.FromUtf16`.

**FormatSuffix**: Writes newline `\n`. If entry has `Exception`, writes exception `ToString()` on next line.

### 4.3 `NullLogFormatter`

File: `src/Logsmith/Formatting/NullLogFormatter.cs` (new)

```csharp
public sealed class NullLogFormatter : ILogFormatter
{
    public static readonly NullLogFormatter Instance = new();
    public void FormatPrefix(in LogEntry entry, IBufferWriter<byte> output) { }
    public void FormatSuffix(in LogEntry entry, IBufferWriter<byte> output) { }
}
```

For sinks that don't want any formatting envelope (raw message only).

### 4.4 Wire into `LogConfigBuilder`

File: `src/Logsmith/LogConfigBuilder.cs`

```csharp
public ILogFormatter? DefaultFormatter { get; set; }
```

When null, sinks use their own defaults. Passed into `LogConfig`, then into sink constructors or set post-construction.

Update convenience methods:

```csharp
public void AddConsoleSink(bool colored = true, ILogFormatter? formatter = null);
public void AddFileSink(string path, ILogFormatter? formatter = null, ...);
```

### 4.5 Refactor `ConsoleSink`

File: `src/Logsmith/Sinks/ConsoleSink.cs`

Remove inline prefix formatting. Add `ILogFormatter` field. In `WriteMessage`:

```csharp
protected override void WriteMessage(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
{
    var buffer = new ArrayBufferWriter<byte>(256);
    _formatter.FormatPrefix(in entry, buffer);
    // write color + prefix + message + suffix + reset
}
```

Constructor: `public ConsoleSink(bool colored = true, LogLevel minimumLevel = LogLevel.Trace, ILogFormatter? formatter = null)`. Defaults to `new DefaultLogFormatter(includeDate: false)`.

### 4.6 Refactor `FileSink`

File: `src/Logsmith/Sinks/FileSink.cs`

Remove inline prefix formatting from `WriteBufferedAsync`. Add `ILogFormatter` field. In `WriteBufferedAsync`, reconstruct a `LogEntry` from `BufferedEntry` fields, format prefix/suffix via `ILogFormatter`, write all parts to the file stream.

Constructor gains `ILogFormatter? formatter = null` parameter. Defaults to `new DefaultLogFormatter(includeDate: true)`.

### 4.7 Refactor `DebugSink`

File: `src/Logsmith/Sinks/DebugSink.cs`

Add optional `ILogFormatter` field. Format prefix+message+suffix into a single string for `Debug.WriteLine`.

### 4.8 Tests

File: `tests/Logsmith.Tests/Formatting/DefaultLogFormatterTests.cs` (new)

| Test | Asserts |
|------|---------|
| `FormatPrefix_TimeOnly_MatchesConsoleFormat` | Output matches `[HH:mm:ss.fff LVL Category] ` pattern |
| `FormatPrefix_WithDate_MatchesFileFormat` | Output matches `[yyyy-MM-dd HH:mm:ss.fff LVL Category] ` |
| `FormatPrefix_WithThread_IncludesThreadId` | Output contains `T{id}` |
| `FormatSuffix_WritesNewline` | Output ends with `\n` |
| `FormatSuffix_WithException_WritesExceptionAfterNewline` | Exception.ToString() appears in output |
| `FormatPrefix_AllLevels_CorrectTags` | Each LogLevel maps to correct TRC/DBG/INF/WRN/ERR/CRT tag |

File: `tests/Logsmith.Tests/Formatting/NullLogFormatterTests.cs` (new)

| Test | Asserts |
|------|---------|
| `FormatPrefix_WritesNothing` | buffer.WrittenCount == 0 |
| `FormatSuffix_WritesNothing` | buffer.WrittenCount == 0 |

File: `tests/Logsmith.Tests/SinkTests/ConsoleSinkTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `CustomFormatter_UsedForPrefix` | Custom ILogFormatter prefix appears in output |

File: `tests/Logsmith.Tests/SinkTests/FileSinkTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `CustomFormatter_UsedInFileOutput` | File content contains custom prefix |

---

## 5. Stream Sink

**Goal**: General-purpose sink that writes to any `Stream`. Async-buffered via `BufferedLogSink`.

### 5.1 Implement `StreamSink`

File: `src/Logsmith/Sinks/StreamSink.cs` (new)

```csharp
namespace Logsmith.Sinks;

public class StreamSink : BufferedLogSink
{
    public StreamSink(Stream stream, LogLevel minimumLevel = LogLevel.Trace,
                      ILogFormatter? formatter = null, bool leaveOpen = false, int capacity = 1024);
    protected override async Task WriteBufferedAsync(BufferedEntry entry, CancellationToken ct);
    protected override async ValueTask OnDisposeAsync();
}
```

**Key behaviors**:
- `leaveOpen`: if true, `OnDisposeAsync` flushes but does not dispose the stream. Useful for `Console.OpenStandardOutput()` or network streams owned by the caller.
- `WriteBufferedAsync`: Reconstruct `LogEntry` from `BufferedEntry`. Use `ArrayBufferWriter<byte>` + `ILogFormatter` for prefix/suffix. Write prefix + message + suffix to stream. Flush.
- Formatter defaults to `new DefaultLogFormatter(includeDate: true)` if null.

### 5.2 Wire into `LogConfigBuilder`

```csharp
public void AddStreamSink(Stream stream, bool leaveOpen = false, ILogFormatter? formatter = null);
```

### 5.3 Tests

File: `tests/Logsmith.Tests/SinkTests/StreamSinkTests.cs` (new)

| Test | Asserts |
|------|---------|
| `Write_WritesToUnderlyingStream` | MemoryStream contains written message |
| `Write_UsesFormatter_ForPrefixSuffix` | MemoryStream output starts with formatted prefix |
| `Write_NullFormatter_WritesRawMessage` | NullLogFormatter → only message bytes, no prefix |
| `LeaveOpen_True_DoesNotDisposeStream` | After sink dispose, stream is still writable |
| `LeaveOpen_False_DisposesStream` | After sink dispose, stream.Write throws ObjectDisposedException |
| `IsEnabled_RespectsMinimumLevel` | Level below minimum → IsEnabled returns false |
| `MultipleWrites_AllAppearInStream` | 3 writes → all 3 messages present in stream |

---

## 6. Multi-Process File Coordination

**Goal**: Optional `FileShare.ReadWrite` mode on `FileSink` so multiple processes can safely append to the same log file.

### 6.1 Modify `FileSink` Constructor

File: `src/Logsmith/Sinks/FileSink.cs`

Add `bool shared` parameter:

```csharp
public FileSink(string path, LogLevel minimumLevel = LogLevel.Trace,
                long maxFileSizeBytes = 10 * 1024 * 1024,
                ILogFormatter? formatter = null, bool shared = false);
```

Store `_shared` field.

### 6.2 Modify `EnsureFileOpen`

When `_shared == true`:

```csharp
_fileStream = new FileStream(_basePath, FileMode.Append, FileAccess.Write,
    FileShare.ReadWrite, bufferSize: 4096);
```

When `_shared == false`: current behavior (`FileShare.Read`).

### 6.3 Seek-to-End Before Write (Shared Mode)

In shared mode, other processes may have appended since our last write. Before writing, `_fileStream.Seek(0, SeekOrigin.End)` to get the true current position. Update `_currentSize = _fileStream.Position` before the size-check for rolling.

This ensures the size-based rolling threshold accounts for bytes written by other processes.

### 6.4 Roll Safety in Shared Mode

Rolling (rename + reopen) is inherently racy in multi-process. In shared mode, wrap `File.Move` in a try/catch:
- If the rename fails (another process already rolled), just reopen the base path — the other process created a fresh file.
- Use `File.Move` with `overwrite: false` to avoid clobbering.

### 6.5 Wire into `LogConfigBuilder`

```csharp
public void AddFileSink(string path, ILogFormatter? formatter = null, bool shared = false, ...);
```

### 6.6 Tests

File: `tests/Logsmith.Tests/SinkTests/FileSinkTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `SharedMode_OpensWithReadWriteShare` | Two FileSink instances on same file don't throw on open |
| `SharedMode_BothProcessesWrite` | Two sinks write, dispose both, file contains both messages |
| `SharedMode_SizeTracking_AccountsForExternalWrites` | Sink A writes, Sink B writes, Sink A's next write sees updated size |
| `SharedMode_RollFailure_ReopensGracefully` | Simulate concurrent roll → sink recovers and continues writing |
| `NonSharedMode_DefaultBehavior_Unchanged` | `shared: false` → same FileShare.Read as before |

---

## 7. Time-Interval Rolling

**Goal**: Extend `FileSink` with optional time-based file rotation (hourly, daily) alongside existing size-based rotation.

### 7.1 Define `RollingInterval` Enum

File: `src/Logsmith/Sinks/RollingInterval.cs` (new)

```csharp
namespace Logsmith.Sinks;

public enum RollingInterval
{
    None,
    Hourly,
    Daily
}
```

### 7.2 Modify `FileSink` Constructor

```csharp
public FileSink(string path, LogLevel minimumLevel = LogLevel.Trace,
                long maxFileSizeBytes = 10 * 1024 * 1024,
                ILogFormatter? formatter = null, bool shared = false,
                RollingInterval rollingInterval = RollingInterval.None);
```

New fields:

```csharp
private readonly RollingInterval _rollingInterval;
private DateTime _currentPeriodStart;  // UTC, truncated to hour/day boundary
```

### 7.3 Period Boundary Calculation

```csharp
private static DateTime GetPeriodStart(DateTime utcNow, RollingInterval interval)
```

- `Hourly`: truncate to hour (`new DateTime(y, M, d, H, 0, 0, UTC)`)
- `Daily`: truncate to day (`utcNow.Date` as UTC)
- `None`: `DateTime.MinValue`

### 7.4 Time-Based Roll Check

In `WriteBufferedAsync`, before the existing size-based roll check:

```csharp
if (_rollingInterval != RollingInterval.None)
{
    var entryTime = new DateTime(entry.TimestampTicks, DateTimeKind.Utc);
    var entryPeriod = GetPeriodStart(entryTime, _rollingInterval);
    if (entryPeriod > _currentPeriodStart)
    {
        await RollFileAsync(ct);
        _currentPeriodStart = entryPeriod;
    }
}
```

Time roll happens first; size roll happens after (if a new period file also exceeds size, it rolls again with a timestamp suffix).

### 7.5 Rolled File Naming

Modify `RollFileAsync` to include interval-aware naming:

- Size-only roll: `app.20260218-143022.log` (current behavior, timestamp to second)
- Time roll (daily): `app.2026-02-18.log`
- Time roll (hourly): `app.2026-02-18-14.log`
- Size roll within a time period: `app.2026-02-18.1.log`, `app.2026-02-18.2.log` (sequence counter)

Algorithm: The active file is always `app.log`. On time roll, rename current to period-stamped name. On size roll within same period, append incrementing sequence number. Track `_sizeRollCount` per period, reset on time roll.

### 7.6 Wire into `LogConfigBuilder`

```csharp
public void AddFileSink(string path, ILogFormatter? formatter = null,
    bool shared = false, RollingInterval rollingInterval = RollingInterval.None,
    long maxFileSizeBytes = 10 * 1024 * 1024);
```

### 7.7 Tests

File: `tests/Logsmith.Tests/SinkTests/FileSinkTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `RollingInterval_Daily_RollsOnDayBoundary` | Write entry at day N, then entry at day N+1 → two files, rolled file named with date |
| `RollingInterval_Hourly_RollsOnHourBoundary` | Write at hour H, then H+1 → rolled file named with hour |
| `RollingInterval_None_NoTimeRoll` | Entries hours apart → single file (no time-based roll) |
| `RollingInterval_SizeAndTime_BothApply` | Exceed size within same period → size roll. Cross period → time roll. Both file patterns present. |
| `RollingInterval_Daily_FileNaming` | Rolled file matches `app.2026-02-18.log` pattern |
| `RollingInterval_Hourly_FileNaming` | Rolled file matches `app.2026-02-18-14.log` pattern |
| `RollingInterval_SizeRollWithinPeriod_SequenceNumber` | Two size rolls in same period → `.1.log`, `.2.log` suffixes |
| `RollingInterval_PeriodChange_ResetsSequence` | Size roll counter resets on new time period |

**Test helper**: To test time-based rolling without waiting real time, create `BufferedEntry` instances with explicit `TimestampTicks` values spanning period boundaries. The roll logic uses `entry.TimestampTicks`, not wall-clock time.

---

## Cross-Cutting: Standalone Mode

All new runtime types (`ILogFormatter`, `DefaultLogFormatter`, `NullLogFormatter`, `StreamSink`, `RollingInterval`) must be embedded by `EmbeddedSourceEmitter` in standalone mode. Verify:

File: `src/Logsmith.Generator/Emission/EmbeddedSourceEmitter.cs`

The emitter reads all `.cs` files from the Logsmith assembly's embedded resources. New files in `src/Logsmith/` are automatically included if the build embeds them. Confirm that `ReplaceVisibility` correctly handles the new types (all are `public` classes/interfaces/enums → become `internal`).

### Standalone Mode Tests

File: `tests/Logsmith.Generator.Tests/StandaloneModeTests.cs` (extend)

| Test | Asserts |
|------|---------|
| `StandaloneMode_EmbedsILogFormatter` | Generated sources contain `internal interface ILogFormatter` |
| `StandaloneMode_EmbedsDefaultLogFormatter` | Generated sources contain `internal sealed class DefaultLogFormatter` |
| `StandaloneMode_EmbedsStreamSink` | Generated sources contain `internal class StreamSink` |
| `StandaloneMode_EmbedsRollingInterval` | Generated sources contain `internal enum RollingInterval` |

---

## Cross-Cutting: `Utf8LogWriter.Write(ReadOnlySpan<byte>)` Overflow Handling

File: `src/Logsmith/Utf8LogWriter.cs`

Currently, `Write` silently drops data that exceeds remaining buffer. The `:json` specifier (Feature 3) can produce large output. No change needed — the existing truncation behavior is correct. Document in the `:json` specifier section that oversized JSON output is truncated in the text path (structured path via `Utf8JsonWriter` has no such limit).

---

## Summary of New Files

| File | Type |
|------|------|
| `src/Logsmith/Formatting/ILogFormatter.cs` | Interface |
| `src/Logsmith/Formatting/DefaultLogFormatter.cs` | Class |
| `src/Logsmith/Formatting/NullLogFormatter.cs` | Class |
| `src/Logsmith/Sinks/StreamSink.cs` | Class |
| `src/Logsmith/Sinks/RollingInterval.cs` | Enum |
| `tests/Logsmith.Tests/LogEntryTests.cs` | Test fixture |
| `tests/Logsmith.Tests/Utf8LogWriterTests.cs` | Test fixture |
| `tests/Logsmith.Tests/Formatting/DefaultLogFormatterTests.cs` | Test fixture |
| `tests/Logsmith.Tests/Formatting/NullLogFormatterTests.cs` | Test fixture |
| `tests/Logsmith.Tests/SinkTests/StreamSinkTests.cs` | Test fixture |

## Summary of Modified Files

| File | Changes |
|------|---------|
| `src/Logsmith/LogEntry.cs` | +ThreadId, +ThreadName fields |
| `src/Logsmith/LogManager.cs` | try/catch around sink calls, error handler invocation |
| `src/Logsmith/LogConfigBuilder.cs` | +InternalErrorHandler, +DefaultFormatter, updated convenience methods |
| `src/Logsmith/Internal/LogConfig.cs` | +ErrorHandler field |
| `src/Logsmith/Utf8LogWriter.cs` | +WriteFormatted with format overload |
| `src/Logsmith/Sinks/BufferedLogSink.cs` | +ThreadId/ThreadName in BufferedEntry |
| `src/Logsmith/Sinks/ConsoleSink.cs` | Refactor to use ILogFormatter |
| `src/Logsmith/Sinks/FileSink.cs` | +ILogFormatter, +shared mode, +RollingInterval, refactored prefix |
| `src/Logsmith/Sinks/DebugSink.cs` | +ILogFormatter |
| `src/Logsmith/Sinks/RecordingSink.cs` | +ThreadId/ThreadName in CapturedEntry |
| `src/Logsmith.Generator/Models/TemplatePart.cs` | +FormatSpecifier property |
| `src/Logsmith.Generator/Parsing/TemplateParser.cs` | Parse format specifiers from placeholders |
| `src/Logsmith.Generator/Emission/TextPathEmitter.cs` | Emit format-aware WriteFormatted calls, :json path |
| `src/Logsmith.Generator/Emission/StructuredPathEmitter.cs` | Format-aware property writes, :json serialization |
| `src/Logsmith.Generator/Emission/MethodEmitter.cs` | Thread info in LogEntry construction, buffer size for :json |
| `src/Logsmith.Generator/Emission/EmbeddedSourceEmitter.cs` | Verify new files embedded correctly |
| `src/Logsmith.Generator/Diagnostics/DiagnosticDescriptors.cs` | +LSMITH006 |
| `tests/Logsmith.Tests/SinkTests/RecordingSinkTests.cs` | Thread info assertions |
| `tests/Logsmith.Tests/SinkTests/FileSinkTests.cs` | Shared mode, rolling interval, formatter tests |
| `tests/Logsmith.Tests/SinkTests/ConsoleSinkTests.cs` | Custom formatter test |
| `tests/Logsmith.Tests/LogManagerTests.cs` | Error handler tests |
| `tests/Logsmith.Generator.Tests/TemplateParsingTests.cs` | Format specifier parsing tests |
| `tests/Logsmith.Generator.Tests/CodeEmissionTests.cs` | Format emission, thread info emission tests |
| `tests/Logsmith.Generator.Tests/DiagnosticTests.cs` | LSMITH006 test |
| `tests/Logsmith.Generator.Tests/StandaloneModeTests.cs` | New type embedding tests |
