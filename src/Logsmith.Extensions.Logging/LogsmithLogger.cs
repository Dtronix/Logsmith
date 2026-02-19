using System.Text;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;

namespace Logsmith.Extensions.Logging;

internal sealed class LogsmithLogger : ILogger
{
    private readonly string _category;

    internal LogsmithLogger(string category)
    {
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        if (state is IEnumerable<KeyValuePair<string, object>> kvps)
        {
            LogScope? first = null;
            foreach (var kvp in kvps)
            {
                var scope = LogScope.Push(kvp.Key, kvp.Value?.ToString() ?? "");
                first ??= scope;
            }
            return first;
        }

        return LogScope.Push("Scope", state.ToString() ?? "");
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

        var entry = new LogEntry(
            level: level,
            eventId: eventId.Id,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: _category,
            exception: exception,
            threadId: Environment.CurrentManagedThreadId,
            threadName: Thread.CurrentThread.Name);

        // Encode message to UTF-8 via stackalloc
        Span<byte> buffer = stackalloc byte[Math.Min(message.Length * 3, 4096)];
        var status = Utf8.FromUtf16(message, buffer, out _, out int bytesWritten);
        var utf8Message = buffer[..bytesWritten];

        LogManager.Dispatch(in entry, utf8Message, message, static (writer, msg) =>
        {
            writer.WriteString("message", msg);
        });
    }

    private static Logsmith.LogLevel MapLevel(Microsoft.Extensions.Logging.LogLevel level)
    {
        return (Logsmith.LogLevel)(int)level;
    }
}
