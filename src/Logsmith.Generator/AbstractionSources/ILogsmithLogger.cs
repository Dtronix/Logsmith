namespace Logsmith;

/// <summary>
/// Logging interface for abstraction mode. Consumers implement this
/// to receive log entries from libraries using Logsmith source generation.
/// </summary>
public interface ILogsmithLogger
{
    /// <summary>
    /// Returns true if the specified level and category should be logged.
    /// Called before any formatting work to enable fast-path exit.
    /// </summary>
    bool IsEnabled(LogLevel level, string category);

    /// <summary>
    /// Writes a log entry with a pre-formatted UTF-8 message.
    /// </summary>
    void Write(in LogEntry entry, System.ReadOnlySpan<byte> utf8Message);
}
