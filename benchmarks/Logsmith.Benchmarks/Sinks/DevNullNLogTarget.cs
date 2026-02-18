using NLog;
using NLog.Targets;

namespace Logsmith.Benchmarks.Sinks;

/// <summary>
/// NLog target that renders messages then discards them.
/// Reads FormattedMessage to ensure the full formatting pipeline executes.
/// </summary>
[Target("DevNull")]
public sealed class DevNullNLogTarget : TargetWithLayout
{
    protected override void Write(LogEventInfo logEvent)
    {
        _ = logEvent.FormattedMessage;
    }
}
