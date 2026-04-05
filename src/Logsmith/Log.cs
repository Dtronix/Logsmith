using System.Diagnostics;
using System.Runtime.CompilerServices;
using Logsmith.Handlers;

namespace Logsmith;

/// <summary>
/// Static logging class with [Conditional] methods for compile-time stripping.
/// Trace and Debug calls are removed entirely when the corresponding symbols
/// (LOGSMITH_TRACE, LOGSMITH_DEBUG) are not defined — achieving 0ns cost.
/// </summary>
public static class Log
{
    [Conditional("LOGSMITH_TRACE")]
    public static void Trace(ILogger logger,
        [InterpolatedStringHandlerArgument("logger")] ref LogTraceHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Trace,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    [Conditional("LOGSMITH_TRACE")]
    public static void Trace(ILogger logger, Exception? exception,
        [InterpolatedStringHandlerArgument("logger", "exception")] ref LogTraceHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Trace,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    [Conditional("LOGSMITH_DEBUG")]
    public static void Debug(ILogger logger,
        [InterpolatedStringHandlerArgument("logger")] ref LogDebugHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Debug,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    [Conditional("LOGSMITH_DEBUG")]
    public static void Debug(ILogger logger, Exception? exception,
        [InterpolatedStringHandlerArgument("logger", "exception")] ref LogDebugHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Debug,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    public static void Information(ILogger logger,
        [InterpolatedStringHandlerArgument("logger")] ref LogInformationHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    public static void Information(ILogger logger, Exception? exception,
        [InterpolatedStringHandlerArgument("logger", "exception")] ref LogInformationHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    public static void Warning(ILogger logger,
        [InterpolatedStringHandlerArgument("logger")] ref LogWarningHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Warning,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    public static void Warning(ILogger logger, Exception? exception,
        [InterpolatedStringHandlerArgument("logger", "exception")] ref LogWarningHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Warning,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    public static void Error(ILogger logger,
        [InterpolatedStringHandlerArgument("logger")] ref LogErrorHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Error,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    public static void Error(ILogger logger, Exception? exception,
        [InterpolatedStringHandlerArgument("logger", "exception")] ref LogErrorHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Error,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    public static void Critical(ILogger logger,
        [InterpolatedStringHandlerArgument("logger")] ref LogCriticalHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Critical,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }

    public static void Critical(ILogger logger, Exception? exception,
        [InterpolatedStringHandlerArgument("logger", "exception")] ref LogCriticalHandler handler)
    {
        if (!handler.IsEnabled) return;
        var info = new DispatchInfo
        {
            Level = LogLevel.Critical,
            Utf8Message = handler.GetTextWritten(),
            Utf8Json = handler.GetJsonWritten(),
            Exception = handler.Exception,
        };
        logger.Context.Dispatch(in info);
    }
}
