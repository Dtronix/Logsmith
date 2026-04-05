using System.Runtime.CompilerServices;

namespace Logsmith.Handlers;

[InterpolatedStringHandler]
public ref struct LogTraceHandler
{
    private LogHandlerCore _core;

    public bool IsEnabled => _core.IsEnabled;
    public Exception? Exception => _core.Exception;
    public ReadOnlySpan<byte> GetTextWritten() => _core.GetTextWritten();
    public ReadOnlySpan<byte> GetJsonWritten() => _core.GetJsonWritten();

    public LogTraceHandler(int literalLength, int formattedCount, ILogger logger, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Trace, out isEnabled);

    public LogTraceHandler(int literalLength, int formattedCount, ILogger logger, Exception? exception, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Trace, out isEnabled, exception);

    public void AppendLiteral(string s) => _core.AppendLiteral(s);
    public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
    public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, format, name);
    public void AppendFormatted(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
}

[InterpolatedStringHandler]
public ref struct LogDebugHandler
{
    private LogHandlerCore _core;

    public bool IsEnabled => _core.IsEnabled;
    public Exception? Exception => _core.Exception;
    public ReadOnlySpan<byte> GetTextWritten() => _core.GetTextWritten();
    public ReadOnlySpan<byte> GetJsonWritten() => _core.GetJsonWritten();

    public LogDebugHandler(int literalLength, int formattedCount, ILogger logger, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Debug, out isEnabled);

    public LogDebugHandler(int literalLength, int formattedCount, ILogger logger, Exception? exception, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Debug, out isEnabled, exception);

    public void AppendLiteral(string s) => _core.AppendLiteral(s);
    public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
    public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, format, name);
    public void AppendFormatted(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
}

[InterpolatedStringHandler]
public ref struct LogInformationHandler
{
    private LogHandlerCore _core;

    public bool IsEnabled => _core.IsEnabled;
    public Exception? Exception => _core.Exception;
    public ReadOnlySpan<byte> GetTextWritten() => _core.GetTextWritten();
    public ReadOnlySpan<byte> GetJsonWritten() => _core.GetJsonWritten();

    public LogInformationHandler(int literalLength, int formattedCount, ILogger logger, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Information, out isEnabled);

    public LogInformationHandler(int literalLength, int formattedCount, ILogger logger, Exception? exception, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Information, out isEnabled, exception);

    public void AppendLiteral(string s) => _core.AppendLiteral(s);
    public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
    public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, format, name);
    public void AppendFormatted(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
}

[InterpolatedStringHandler]
public ref struct LogWarningHandler
{
    private LogHandlerCore _core;

    public bool IsEnabled => _core.IsEnabled;
    public Exception? Exception => _core.Exception;
    public ReadOnlySpan<byte> GetTextWritten() => _core.GetTextWritten();
    public ReadOnlySpan<byte> GetJsonWritten() => _core.GetJsonWritten();

    public LogWarningHandler(int literalLength, int formattedCount, ILogger logger, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Warning, out isEnabled);

    public LogWarningHandler(int literalLength, int formattedCount, ILogger logger, Exception? exception, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Warning, out isEnabled, exception);

    public void AppendLiteral(string s) => _core.AppendLiteral(s);
    public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
    public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, format, name);
    public void AppendFormatted(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
}

[InterpolatedStringHandler]
public ref struct LogErrorHandler
{
    private LogHandlerCore _core;

    public bool IsEnabled => _core.IsEnabled;
    public Exception? Exception => _core.Exception;
    public ReadOnlySpan<byte> GetTextWritten() => _core.GetTextWritten();
    public ReadOnlySpan<byte> GetJsonWritten() => _core.GetJsonWritten();

    public LogErrorHandler(int literalLength, int formattedCount, ILogger logger, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Error, out isEnabled);

    public LogErrorHandler(int literalLength, int formattedCount, ILogger logger, Exception? exception, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Error, out isEnabled, exception);

    public void AppendLiteral(string s) => _core.AppendLiteral(s);
    public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
    public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, format, name);
    public void AppendFormatted(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
}

[InterpolatedStringHandler]
public ref struct LogCriticalHandler
{
    private LogHandlerCore _core;

    public bool IsEnabled => _core.IsEnabled;
    public Exception? Exception => _core.Exception;
    public ReadOnlySpan<byte> GetTextWritten() => _core.GetTextWritten();
    public ReadOnlySpan<byte> GetJsonWritten() => _core.GetJsonWritten();

    public LogCriticalHandler(int literalLength, int formattedCount, ILogger logger, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Critical, out isEnabled);

    public LogCriticalHandler(int literalLength, int formattedCount, ILogger logger, Exception? exception, out bool isEnabled)
        => _core = new LogHandlerCore(literalLength, formattedCount, logger, LogLevel.Critical, out isEnabled, exception);

    public void AppendLiteral(string s) => _core.AppendLiteral(s);
    public void AppendFormatted<T>(T value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
    public void AppendFormatted<T>(T value, string? format, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, format, name);
    public void AppendFormatted(string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        => _core.AppendFormatted(value, name);
}
