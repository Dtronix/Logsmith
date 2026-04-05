using System.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Logsmith.Internal;

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
        DispatchTiming(LogLevel.Information, "completed", elapsed.TotalMilliseconds, null, null);
    }

    /// <summary>
    /// Logs failure with elapsed time and exception.
    /// </summary>
    public void Fail(Exception exception)
    {
        if (_completed) return;
        _completed = true;

        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        DispatchTiming(LogLevel.Error, "failed", elapsed.TotalMilliseconds, exception, null);
    }

    /// <summary>
    /// Logs an intermediate timing step.
    /// </summary>
    public void TimeStep(string stepName)
    {
        var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
        DispatchTiming(LogLevel.Debug, "step", elapsed.TotalMilliseconds, null, stepName);
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
            DispatchTiming(LogLevel.Warning, "abandoned", elapsed.TotalMilliseconds, null, null);
        }

        _context.PathSegment = null;
    }

    private void DispatchTiming(LogLevel level, string outcome, double elapsedMs,
        Exception? exception, string? stepName)
    {
        if (!_context.IsEnabled(level)) return;

        // Text buffer — human-readable message
        var textBuffer = ThreadBuffer.GetHandlerText();
        string textMessage;
        if (stepName is not null)
            textMessage = $"Operation '{_operationName}' step '{stepName}' at {elapsedMs:F1}ms";
        else if (outcome == "completed")
            textMessage = $"Operation '{_operationName}' completed in {elapsedMs:F1}ms";
        else if (outcome == "failed")
            textMessage = $"Operation '{_operationName}' failed after {elapsedMs:F1}ms";
        else
            textMessage = $"Operation '{_operationName}' abandoned after {elapsedMs:F1}ms";

        Encoding.UTF8.GetBytes(textMessage.AsSpan(), textBuffer);

        // JSON buffer — structured properties
        var jsonBuffer = ThreadBuffer.GetHandlerJson();
        var jsonWriter = ThreadBuffer.GetJsonWriter(jsonBuffer);
        jsonWriter.WriteStartObject();
        jsonWriter.WriteString("operation", _operationName);
        jsonWriter.WriteString("outcome", outcome);
        jsonWriter.WriteNumber("elapsed_ms", Math.Round(elapsedMs, 1));
        if (stepName is not null)
            jsonWriter.WriteString("step", stepName);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var info = new DispatchInfo
        {
            Level = level,
            Utf8Message = textBuffer.WrittenSpan,
            Utf8Json = jsonBuffer.WrittenSpan,
            Exception = exception,
        };
        _context.Dispatch(in info);
    }
}
