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
    /// Writes a log dispatch with pre-formatted UTF-8 text and structured JSON.
    /// </summary>
    void Write(in DispatchInfo info);
}
