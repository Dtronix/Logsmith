using Microsoft.Extensions.Logging;

namespace Logsmith.Benchmarks.Sinks;

/// <summary>
/// MEL logger provider that renders messages then discards them.
/// Calls formatter(state, exception) to ensure the full formatting pipeline executes.
/// </summary>
public sealed class DevNullMelLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new DevNullMelLogger();

    public void Dispose() { }

    private sealed class DevNullMelLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            // Force message rendering for fair comparison.
            _ = formatter(state, exception);
        }
    }
}
