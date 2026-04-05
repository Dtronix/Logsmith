using System.Runtime.CompilerServices;
using System.Text;
using Logsmith.Handlers;

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

    /// <summary>
    /// Logs at Error level, then throws <see cref="InvalidOperationException"/> if
    /// <see cref="LogConfigBuilder.ThrowOnDPanic"/> is enabled. Use for conditions
    /// that should never occur — fail-fast in dev/test, log-and-continue in production.
    /// </summary>
    void DPanic(string message)
    {
        DispatchString(LogLevel.Error, message, null);
        if (LogManager.ShouldThrowOnDPanic())
            throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Logs at Error level with an exception, then throws <see cref="InvalidOperationException"/>
    /// if <see cref="LogConfigBuilder.ThrowOnDPanic"/> is enabled.
    /// </summary>
    void DPanic(string message, Exception? exception)
    {
        DispatchString(LogLevel.Error, message, exception);
        if (LogManager.ShouldThrowOnDPanic())
            throw new InvalidOperationException(message, exception);
    }

    /// <summary>
    /// Handler-based DPanic — logs at Error level with structured data, then throws
    /// if <see cref="LogConfigBuilder.ThrowOnDPanic"/> is enabled.
    /// </summary>
    void DPanic([InterpolatedStringHandlerArgument("")] ref LogErrorHandler handler)
    {
        DispatchHandler(LogLevel.Error, ref handler);
        if (LogManager.ShouldThrowOnDPanic())
            throw new InvalidOperationException(Encoding.UTF8.GetString(handler.GetTextWritten()));
    }

    /// <summary>
    /// Handler-based DPanic with exception — logs at Error level with structured data,
    /// then throws if <see cref="LogConfigBuilder.ThrowOnDPanic"/> is enabled.
    /// </summary>
    void DPanic(Exception? exception,
        [InterpolatedStringHandlerArgument("", "exception")] ref LogErrorHandler handler)
    {
        DispatchHandler(LogLevel.Error, ref handler);
        if (LogManager.ShouldThrowOnDPanic())
            throw new InvalidOperationException(
                Encoding.UTF8.GetString(handler.GetTextWritten()), exception);
    }

    // ── Terminal methods (handler-based) ────────────────────────────────

    void Trace([InterpolatedStringHandlerArgument("")] ref LogTraceHandler handler)
        => DispatchHandler(LogLevel.Trace, ref handler);
    void Trace(Exception? exception,
        [InterpolatedStringHandlerArgument("", "exception")] ref LogTraceHandler handler)
        => DispatchHandler(LogLevel.Trace, ref handler);

    void Debug([InterpolatedStringHandlerArgument("")] ref LogDebugHandler handler)
        => DispatchHandler(LogLevel.Debug, ref handler);
    void Debug(Exception? exception,
        [InterpolatedStringHandlerArgument("", "exception")] ref LogDebugHandler handler)
        => DispatchHandler(LogLevel.Debug, ref handler);

    void Information([InterpolatedStringHandlerArgument("")] ref LogInformationHandler handler)
        => DispatchHandler(LogLevel.Information, ref handler);
    void Information(Exception? exception,
        [InterpolatedStringHandlerArgument("", "exception")] ref LogInformationHandler handler)
        => DispatchHandler(LogLevel.Information, ref handler);

    void Warning([InterpolatedStringHandlerArgument("")] ref LogWarningHandler handler)
        => DispatchHandler(LogLevel.Warning, ref handler);
    void Warning(Exception? exception,
        [InterpolatedStringHandlerArgument("", "exception")] ref LogWarningHandler handler)
        => DispatchHandler(LogLevel.Warning, ref handler);

    void Error([InterpolatedStringHandlerArgument("")] ref LogErrorHandler handler)
        => DispatchHandler(LogLevel.Error, ref handler);
    void Error(Exception? exception,
        [InterpolatedStringHandlerArgument("", "exception")] ref LogErrorHandler handler)
        => DispatchHandler(LogLevel.Error, ref handler);

    void Critical([InterpolatedStringHandlerArgument("")] ref LogCriticalHandler handler)
        => DispatchHandler(LogLevel.Critical, ref handler);
    void Critical(Exception? exception,
        [InterpolatedStringHandlerArgument("", "exception")] ref LogCriticalHandler handler)
        => DispatchHandler(LogLevel.Critical, ref handler);

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

    // ── Private helpers ─────────────────────────────────────────────────

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

    private void DispatchHandler(LogLevel level, ref LogTraceHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        Context.Dispatch(in info);
    }

    private void DispatchHandler(LogLevel level, ref LogDebugHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        Context.Dispatch(in info);
    }

    private void DispatchHandler(LogLevel level, ref LogInformationHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        Context.Dispatch(in info);
    }

    private void DispatchHandler(LogLevel level, ref LogWarningHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        Context.Dispatch(in info);
    }

    private void DispatchHandler(LogLevel level, ref LogErrorHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        Context.Dispatch(in info);
    }

    private void DispatchHandler(LogLevel level, ref LogCriticalHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        Context.Dispatch(in info);
    }
}
