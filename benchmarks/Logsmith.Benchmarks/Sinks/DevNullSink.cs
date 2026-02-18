using Logsmith;
using Logsmith.Sinks;

namespace Logsmith.Benchmarks.Sinks;

/// <summary>
/// Logsmith sink that accepts all writes but discards output.
/// IsEnabled always returns true to ensure the full formatting pipeline executes.
/// </summary>
public sealed class DevNullSink : ILogSink
{
    public bool IsEnabled(LogLevel level) => true;

    public void Write(in LogEntry entry, ReadOnlySpan<byte> utf8Message)
    {
        // Force the runtime to observe the span so the write path isn't elided.
        _ = utf8Message.Length;
    }

    public void Dispose() { }
}
