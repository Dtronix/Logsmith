# Compile-Time Diagnostics

The generator produces the following diagnostics:

| Code | Severity | Description |
|---|---|---|
| LSMITH001 | Error | Placeholder `{name}` in message template has no matching parameter. |
| LSMITH002 | Warning | Parameter is not referenced in the message template and is not a special type (Exception, caller info, ILogSink). |
| LSMITH003 | Error | Log method must be `static partial` in a `partial` class. |
| LSMITH004 | Error | Parameter type does not implement `IUtf8SpanFormattable`, `ISpanFormattable`, `IFormattable`, or `ToString()`. |
| LSMITH005 | Warning | Parameter has a `[Caller*]` attribute and also appears in the message template. Caller attribute takes priority. |
| LSMITH006 | Warning | `:json` format specifier on primitive type is unnecessary — prefer default formatting. |
| LSMITH007 | Warning | Both `SampleRate` and `MaxPerSecond` are set on the same method. `SampleRate` is applied first. |
| LSMITH008 | Warning | `ILogSink` explicit sink parameter in abstraction mode — use `ILogsmithLogger` instead. |
| LSMITH010 | Warning | `LogsmithMode` is Standalone or Abstraction but `PrivateAssets="all"` is missing on the Logsmith package reference. |
