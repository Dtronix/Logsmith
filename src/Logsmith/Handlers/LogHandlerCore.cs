using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Logsmith.Internal;

namespace Logsmith.Handlers;

/// <summary>
/// Shared implementation for all level-specific interpolated string handlers.
/// Ref struct — lives on the stack, no allocation when disabled.
/// Writes both UTF-8 text and structured JSON simultaneously.
/// </summary>
public ref struct LogHandlerCore
{
    private ArrayBufferWriter<byte>? _textBuffer;
    private ArrayBufferWriter<byte>? _jsonBuffer;
    private Utf8JsonWriter? _jsonWriter;
    private bool _enabled;
    private bool _jsonFinalized;
    private int _propertyCount;
    private Exception? _exception;

    public bool IsEnabled => _enabled;
    public Exception? Exception => _exception;

    public LogHandlerCore(int literalLength, int formattedCount,
        ILogger logger, LogLevel level, out bool isEnabled, Exception? exception = null)
    {
        _enabled = isEnabled = logger.IsEnabled(level);
        _exception = exception;
        if (!_enabled) return;

        _textBuffer = ThreadBuffer.GetHandlerText();

        if (formattedCount > 0)
        {
            _jsonBuffer = ThreadBuffer.GetHandlerJson();
            _jsonWriter = ThreadBuffer.GetJsonWriter(_jsonBuffer);
            _jsonWriter.WriteStartObject();
        }
    }

    public void AppendLiteral(string s)
    {
        if (!_enabled) return;
        Encoding.UTF8.GetBytes(s.AsSpan(), _textBuffer!);
    }

    public void AppendFormatted<T>(T value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (!_enabled) return;
        _propertyCount++;

        // Text path
        var str = value?.ToString() ?? "null";
        Encoding.UTF8.GetBytes(str.AsSpan(), _textBuffer!);

        // Structured path
        if (_jsonWriter is not null)
        {
            var propertyName = name ?? $"arg{_propertyCount}";
            WriteJsonProperty(_jsonWriter, propertyName, value);
        }
    }

    public void AppendFormatted<T>(T value, string? format,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (!_enabled) return;
        _propertyCount++;

        // Text path (with format)
        var str = format is not null && value is IFormattable f
            ? f.ToString(format, null)
            : value?.ToString() ?? "null";
        Encoding.UTF8.GetBytes(str.AsSpan(), _textBuffer!);

        // Structured path
        if (_jsonWriter is not null)
        {
            var propertyName = name ?? $"arg{_propertyCount}";
            WriteJsonProperty(_jsonWriter, propertyName, value);
        }
    }

    /// <summary>
    /// String specialization — compiler prefers this for string arguments.
    /// </summary>
    public void AppendFormatted(string? value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (!_enabled) return;
        _propertyCount++;

        // Text path
        Encoding.UTF8.GetBytes((value ?? "null").AsSpan(), _textBuffer!);

        // Structured path
        if (_jsonWriter is not null)
        {
            var propertyName = name ?? $"arg{_propertyCount}";
            _jsonWriter.WriteString(propertyName, value);
        }
    }

    public ReadOnlySpan<byte> GetTextWritten()
        => _textBuffer is not null ? _textBuffer.WrittenSpan : default;

    public ReadOnlySpan<byte> GetJsonWritten()
    {
        if (_jsonWriter is null) return default;
        if (_jsonFinalized) return _jsonBuffer!.WrittenSpan;
        _jsonFinalized = true;
        _jsonWriter.WriteEndObject();
        _jsonWriter.Flush();
        return _jsonBuffer!.WrittenSpan;
    }

    /// <summary>
    /// JIT-specialized: typeof(T) comparisons become constants,
    /// dead branches are eliminated. Zero boxing for primitive types.
    /// </summary>
    private static void WriteJsonProperty<T>(Utf8JsonWriter writer, string name, T value)
    {
        if (typeof(T) == typeof(int))
            writer.WriteNumber(name, Unsafe.As<T, int>(ref value));
        else if (typeof(T) == typeof(long))
            writer.WriteNumber(name, Unsafe.As<T, long>(ref value));
        else if (typeof(T) == typeof(double))
            writer.WriteNumber(name, Unsafe.As<T, double>(ref value));
        else if (typeof(T) == typeof(float))
            writer.WriteNumber(name, Unsafe.As<T, float>(ref value));
        else if (typeof(T) == typeof(decimal))
            writer.WriteNumber(name, Unsafe.As<T, decimal>(ref value));
        else if (typeof(T) == typeof(bool))
            writer.WriteBoolean(name, Unsafe.As<T, bool>(ref value));
        else if (typeof(T) == typeof(string))
            writer.WriteString(name, Unsafe.As<T, string>(ref value));
        else if (typeof(T) == typeof(DateTime))
            writer.WriteString(name, Unsafe.As<T, DateTime>(ref value));
        else if (typeof(T) == typeof(DateTimeOffset))
            writer.WriteString(name, Unsafe.As<T, DateTimeOffset>(ref value));
        else if (typeof(T) == typeof(Guid))
            writer.WriteString(name, Unsafe.As<T, Guid>(ref value));
        else
            writer.WriteString(name, value?.ToString());
    }
}
