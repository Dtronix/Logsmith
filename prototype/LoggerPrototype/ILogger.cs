using System.Runtime.CompilerServices;

namespace LoggerPrototype;

public interface ILogger
{
    string Category { get; }
    bool IsEnabled(LogLevel level);

    // --- Handler overloads (compiler picks these for $"...") ---

    void Trace([InterpolatedStringHandlerArgument("")] ref LogTraceHandler handler);
    void Debug([InterpolatedStringHandlerArgument("")] ref LogDebugHandler handler);
    void Information([InterpolatedStringHandlerArgument("")] ref LogInformationHandler handler);
    void Warning([InterpolatedStringHandlerArgument("")] ref LogWarningHandler handler);
    void Error([InterpolatedStringHandlerArgument("")] ref LogErrorHandler handler);
    void Error(Exception? ex, [InterpolatedStringHandlerArgument("", "ex")] ref LogErrorHandler handler);
    void Critical([InterpolatedStringHandlerArgument("")] ref LogCriticalHandler handler);
    void Critical(Exception? ex, [InterpolatedStringHandlerArgument("", "ex")] ref LogCriticalHandler handler);

    // --- String overloads (plain strings, pre-built strings) ---

    void Trace(string message);
    void Debug(string message);
    void Information(string message);
    void Warning(string message);
    void Error(string message);
    void Error(Exception? ex, string message);
    void Critical(string message);
    void Critical(Exception? ex, string message);
}
