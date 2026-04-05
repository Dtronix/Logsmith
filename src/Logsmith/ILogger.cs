using System.Text;

namespace Logsmith;

/// <summary>
/// Primary logging interface. Provides terminal methods (Debug, Information, etc.),
/// chain methods (When, Tagged, etc.), and hierarchy support (CreateChild, PathSegment).
/// Only <see cref="Context"/> must be implemented; all other members have defaults.
/// </summary>
public interface ILogger
{
    /// <summary>
    /// The underlying dispatch context for this logger.
    /// </summary>
    LoggerContext Context { get; }

    /// <summary>
    /// Checks if the given level is enabled for this logger's category.
    /// </summary>
    bool IsEnabled(LogLevel level) => Context.IsEnabled(level);

    // ── Terminal methods (string-based) ─────────────────────────────────
    // Handler-based overloads are added in Phase 4.

    void Trace(string message) => DispatchString(LogLevel.Trace, message, null);
    void Trace(string message, Exception? exception) => DispatchString(LogLevel.Trace, message, exception);

    void Debug(string message) => DispatchString(LogLevel.Debug, message, null);
    void Debug(string message, Exception? exception) => DispatchString(LogLevel.Debug, message, exception);

    void Information(string message) => DispatchString(LogLevel.Information, message, null);
    void Information(string message, Exception? exception) => DispatchString(LogLevel.Information, message, exception);

    void Warning(string message) => DispatchString(LogLevel.Warning, message, null);
    void Warning(string message, Exception? exception) => DispatchString(LogLevel.Warning, message, exception);

    void Error(string message) => DispatchString(LogLevel.Error, message, null);
    void Error(string message, Exception? exception) => DispatchString(LogLevel.Error, message, exception);

    void Critical(string message) => DispatchString(LogLevel.Critical, message, null);
    void Critical(string message, Exception? exception) => DispatchString(LogLevel.Critical, message, exception);

    // ── Chain methods ───────────────────────────────────────────────────
    // Default implementations are stubs. Interceptors (Phase 6) provide
    // full chain functionality with carrier types.

    /// <summary>
    /// Returns this logger if condition is true, otherwise NullLogger.
    /// </summary>
    ILogger When(bool condition) => condition ? this : NullLogger.Instance;

    /// <summary>
    /// Sampling stub — logs every Nth message when interceptors are active.
    /// Default: passes through (no sampling).
    /// </summary>
    ILogger Sampled(int rate) => this;

    /// <summary>
    /// Rate limiting stub — limits to N messages per second when interceptors are active.
    /// Default: passes through (no limiting).
    /// </summary>
    ILogger RateLimited(int maxPerSecond) => this;

    /// <summary>
    /// Tagging stub — attaches a tag when interceptors are active.
    /// Default: passes through (tag is not propagated without interceptors).
    /// </summary>
    ILogger Tagged(string tag) => this;

    // ── Hierarchy ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a child logger with a new path segment appended.
    /// </summary>
    ILogger CreateChild(string? segment) => new LoggerInstance(Context.CreateChild(segment));

    /// <summary>
    /// Gets or sets the path segment for this logger's path node.
    /// </summary>
    string? PathSegment
    {
        get => Context.PathSegment;
        set => Context.PathSegment = value;
    }

    // ── Private helper ──────────────────────────────────────────────────

    private void DispatchString(LogLevel level, string message, Exception? exception)
    {
        if (!IsEnabled(level)) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = bytes,
            Exception = exception,
        };
        Context.Dispatch(in info);
    }
}
