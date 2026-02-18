using Serilog.Core;
using Serilog.Events;

namespace Logsmith.Benchmarks.Sinks;

/// <summary>
/// Serilog sink that renders messages then discards them.
/// Calls RenderMessage() to ensure the full formatting pipeline executes.
/// </summary>
public sealed class DevNullSerilogSink : ILogEventSink
{
    public void Emit(LogEvent logEvent)
    {
        _ = logEvent.RenderMessage();
    }
}
