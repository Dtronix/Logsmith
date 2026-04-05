using System.Text;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;

namespace Logsmith.Extensions.Logging;

internal sealed class LogsmithLogger : Microsoft.Extensions.Logging.ILogger
{
    private readonly string _category;

    internal LogsmithLogger(string category)
    {
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        // LogScope (AsyncLocal) has been removed. Scoping is now explicit via ILogger.Scoped().
        // MEL ambient scoping is not supported — return a no-op disposable.
        return NullScope.Instance;
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
    {
        return LogManager.IsEnabled(MapLevel(logLevel), _category);
    }

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var level = MapLevel(logLevel);
        if (!LogManager.IsEnabled(level, _category))
            return;

        var message = formatter(state, exception);

        // Encode message to UTF-8 via stackalloc
        Span<byte> buffer = stackalloc byte[Math.Min(message.Length * 3, 4096)];
        var status = Utf8.FromUtf16(message, buffer, out _, out int bytesWritten);
        var utf8Message = buffer[..bytesWritten];

        var info = new DispatchInfo
        {
            Level = level,
            EventId = eventId.Id,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = _category,
            Utf8Message = utf8Message,
            Exception = exception,
            ThreadId = Environment.CurrentManagedThreadId,
            ThreadName = Thread.CurrentThread.Name,
        };

        LogManager.Dispatch(in info);
    }

    private static Logsmith.LogLevel MapLevel(Microsoft.Extensions.Logging.LogLevel level)
    {
        return (Logsmith.LogLevel)(int)level;
    }

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
