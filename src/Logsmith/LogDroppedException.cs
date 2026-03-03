namespace Logsmith;

/// <summary>
/// Raised via the internal error handler when a <see cref="Sinks.BufferedLogSink"/>
/// drops a log message because its bounded channel is full.
/// </summary>
public sealed class LogDroppedException : Exception
{
    public long TotalDropped { get; }

    public LogDroppedException(long totalDropped)
        : base($"Log message dropped. Total dropped: {totalDropped}")
        => TotalDropped = totalDropped;
}
