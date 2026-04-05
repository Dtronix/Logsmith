using System.Diagnostics;
using System.Text;

namespace Logsmith;

/// <summary>
/// Struct-based timed operation that logs start/complete/fail/abandon.
/// Use via <see cref="LoggerExtensions.TimeOperation"/>.
/// </summary>
public struct TimingOperation : ILogger, IDisposable
{
    private readonly LoggerContext _context;
    private readonly long _startTimestamp;
    private readonly string _operationName;
    private bool _completed;

    internal TimingOperation(LoggerContext parentContext, string name)
    {
        _context = parentContext.CreateChild(name);
        _startTimestamp = Stopwatch.GetTimestamp();
        _operationName = name;
        _completed = false;
    }

    public LoggerContext Context => _context;

    /// <summary>
    /// Logs successful completion with elapsed time.
    /// </summary>
    public void Complete()
    {
        if (_completed) return;
        _completed = true;

        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        DispatchTiming(LogLevel.Information,
            $"Operation '{_operationName}' completed in {elapsed.TotalMilliseconds:F1}ms", null);
    }

    /// <summary>
    /// Logs failure with elapsed time and exception.
    /// </summary>
    public void Fail(Exception exception)
    {
        if (_completed) return;
        _completed = true;

        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        DispatchTiming(LogLevel.Error,
            $"Operation '{_operationName}' failed after {elapsed.TotalMilliseconds:F1}ms", exception);
    }

    /// <summary>
    /// Logs an intermediate timing step.
    /// </summary>
    public void TimeStep(string stepName)
    {
        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        DispatchTiming(LogLevel.Debug,
            $"Operation '{_operationName}' step '{stepName}' at {elapsed.TotalMilliseconds:F1}ms", null);
    }

    /// <summary>
    /// If neither Complete nor Fail was called, logs abandonment.
    /// Clears the path segment.
    /// </summary>
    public void Dispose()
    {
        if (!_completed)
        {
            _completed = true;
            var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
            DispatchTiming(LogLevel.Warning,
                $"Operation '{_operationName}' abandoned after {elapsed.TotalMilliseconds:F1}ms", null);
        }

        _context.PathSegment = null;
    }

    private void DispatchTiming(LogLevel level, string message, Exception? exception)
    {
        if (!_context.IsEnabled(level)) return;
        var bytes = Encoding.UTF8.GetBytes(message);
        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = bytes,
            Exception = exception,
        };
        _context.Dispatch(in info);
    }
}
