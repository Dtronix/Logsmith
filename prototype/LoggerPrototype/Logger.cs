using System.Text;

namespace LoggerPrototype;

public sealed class Logger : ILogger
{
    private readonly string _category;
    private readonly LogLevel _minimumLevel;

    public string Category => _category;

    public Logger(string category, LogLevel minimumLevel = LogLevel.Trace)
    {
        _category = category;
        _minimumLevel = minimumLevel;
    }

    public bool IsEnabled(LogLevel level) => level >= _minimumLevel;

    // --- Handler overloads (compiler picks these for $"...") ---

    public void Trace(ref LogTraceHandler handler)
    {
        if (!handler.IsEnabled) return;
        WriteOutput(LogLevel.Trace, handler.GetTextWritten(), handler.GetJsonWritten(), null);
    }

    public void Debug(ref LogDebugHandler handler)
    {
        if (!handler.IsEnabled) return;
        WriteOutput(LogLevel.Debug, handler.GetTextWritten(), handler.GetJsonWritten(), null);
    }

    public void Information(ref LogInformationHandler handler)
    {
        if (!handler.IsEnabled) return;
        WriteOutput(LogLevel.Information, handler.GetTextWritten(), handler.GetJsonWritten(), null);
    }

    public void Warning(ref LogWarningHandler handler)
    {
        if (!handler.IsEnabled) return;
        WriteOutput(LogLevel.Warning, handler.GetTextWritten(), handler.GetJsonWritten(), null);
    }

    public void Error(ref LogErrorHandler handler)
    {
        if (!handler.IsEnabled) return;
        WriteOutput(LogLevel.Error, handler.GetTextWritten(), handler.GetJsonWritten(), handler.Exception);
    }

    public void Error(Exception? ex, ref LogErrorHandler handler)
    {
        if (!handler.IsEnabled) return;
        WriteOutput(LogLevel.Error, handler.GetTextWritten(), handler.GetJsonWritten(), handler.Exception ?? ex);
    }

    public void Critical(ref LogCriticalHandler handler)
    {
        if (!handler.IsEnabled) return;
        WriteOutput(LogLevel.Critical, handler.GetTextWritten(), handler.GetJsonWritten(), handler.Exception);
    }

    public void Critical(Exception? ex, ref LogCriticalHandler handler)
    {
        if (!handler.IsEnabled) return;
        WriteOutput(LogLevel.Critical, handler.GetTextWritten(), handler.GetJsonWritten(), handler.Exception ?? ex);
    }

    // --- String overloads (plain strings, pre-built strings) ---

    public void Trace(string message) => DispatchString(LogLevel.Trace, message);
    public void Debug(string message) => DispatchString(LogLevel.Debug, message);
    public void Information(string message) => DispatchString(LogLevel.Information, message);
    public void Warning(string message) => DispatchString(LogLevel.Warning, message);
    public void Error(string message) => DispatchString(LogLevel.Error, message);
    public void Error(Exception? ex, string message) => DispatchString(LogLevel.Error, message, ex);
    public void Critical(string message) => DispatchString(LogLevel.Critical, message);
    public void Critical(Exception? ex, string message) => DispatchString(LogLevel.Critical, message, ex);

    // --- Output ---

    private void WriteOutput(LogLevel level, ReadOnlySpan<byte> textBytes, ReadOnlySpan<byte> jsonBytes, Exception? ex)
    {
        var text = Encoding.UTF8.GetString(textBytes);

        Console.ForegroundColor = GetColor(level);
        Console.Write($"[{DateTime.Now:HH:mm:ss.fff} {LevelTag(level)} {_category}] ");
        Console.ResetColor();
        Console.WriteLine(text);

        if (ex is not null)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"  exception: {ex.GetType().Name}: {ex.Message}");
            Console.ResetColor();
        }

        if (jsonBytes.Length > 0)
        {
            var json = Encoding.UTF8.GetString(jsonBytes);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  structured: {json}");
            Console.ResetColor();
        }
    }

    private void DispatchString(LogLevel level, string message, Exception? ex = null)
    {
        if (!IsEnabled(level)) return;

        Console.ForegroundColor = GetColor(level);
        Console.Write($"[{DateTime.Now:HH:mm:ss.fff} {LevelTag(level)} {_category}] ");
        Console.ResetColor();
        Console.WriteLine(message);

        if (ex is not null)
        {
            Console.ForegroundColor = ConsoleColor.DarkRed;
            Console.WriteLine($"  exception: {ex.GetType().Name}: {ex.Message}");
            Console.ResetColor();
        }

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("  (string overload - no structured properties)");
        Console.ResetColor();
    }

    private static string LevelTag(LogLevel level) => level switch
    {
        LogLevel.Trace => "TRC",
        LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "???",
    };

    private static ConsoleColor GetColor(LogLevel level) => level switch
    {
        LogLevel.Trace => ConsoleColor.DarkGray,
        LogLevel.Debug => ConsoleColor.Gray,
        LogLevel.Information => ConsoleColor.Cyan,
        LogLevel.Warning => ConsoleColor.Yellow,
        LogLevel.Error => ConsoleColor.Red,
        LogLevel.Critical => ConsoleColor.DarkRed,
        _ => ConsoleColor.White,
    };
}
