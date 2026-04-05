using System.Text;
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

        // Encode message to UTF-8 — stackalloc for typical messages, heap fallback for large ones
        int maxBytes = Encoding.UTF8.GetByteCount(message);
        byte[]? rented = null;
        Span<byte> buffer = maxBytes <= 4096
            ? stackalloc byte[maxBytes]
            : (rented = System.Buffers.ArrayPool<byte>.Shared.Rent(maxBytes));
        int bytesWritten = Encoding.UTF8.GetBytes(message, buffer);
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

        if (rented is not null)
            System.Buffers.ArrayPool<byte>.Shared.Return(rented);
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
