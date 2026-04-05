using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace WhenTest;

public interface ILogger
{
    string Name { get; }
    bool IsEnabled(LogLevel level);

    // Handler overload (compiler picks for $"...")
    void Debug([InterpolatedStringHandlerArgument("")] ref LogDebugHandler handler);

    // String overload
    void Debug(string message);

    // Chaining — default interface implementation
    ILogger When(bool condition) => condition ? this : NullLogger.Instance;
}

public enum LogLevel { Trace, Debug, Information, Warning, Error, Critical, None }

// ============================================================
// Concrete logger
// ============================================================
public sealed class Logger : ILogger
{
    public string Name => "Logger";
    public bool IsEnabled(LogLevel level) => level >= LogLevel.Debug;

    public void Debug(ref LogDebugHandler handler)
    {
        Console.WriteLine("  [ORIGINAL Logger.Debug] — should NOT see this if intercepted");
    }

    public void Debug(string message)
    {
        Console.WriteLine($"  [ORIGINAL Logger.Debug(string)] {message}");
    }
}

// ============================================================
// Null logger — returned by When(false)
// ============================================================
public sealed class NullLogger : ILogger
{
    public static readonly NullLogger Instance = new();
    public string Name => "NullLogger";
    public bool IsEnabled(LogLevel level) => false;

    public void Debug(ref LogDebugHandler handler)
    {
        Console.WriteLine("  [ORIGINAL NullLogger.Debug] — should NOT see this if intercepted");
    }

    public void Debug(string message)
    {
        // No-op
    }
}

// ============================================================
// InterpolatedStringHandler
// ============================================================
[InterpolatedStringHandler]
public ref struct LogDebugHandler
{
    private ArrayBufferWriter<byte>? _buffer;
    private bool _enabled;

    public bool IsEnabled => _enabled;

    public LogDebugHandler(int literalLength, int formattedCount,
        ILogger logger, out bool isEnabled)
    {
        _enabled = isEnabled = logger.IsEnabled(LogLevel.Debug);
        Console.WriteLine($"  [HANDLER CTOR] logger={logger.Name}, isEnabled={isEnabled}");

        if (isEnabled)
            _buffer = new ArrayBufferWriter<byte>(literalLength + formattedCount * 64);
    }

    public void AppendLiteral(string s)
    {
        if (!_enabled) return;
        Console.WriteLine($"  [HANDLER] AppendLiteral(\"{s}\")");
        Encoding.UTF8.GetBytes(s.AsSpan(), _buffer!);
    }

    public void AppendFormatted<T>(T value,
        [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (!_enabled) return;
        var str = value?.ToString() ?? "null";
        Console.WriteLine($"  [HANDLER] AppendFormatted({str}, name=\"{name}\")");
        Encoding.UTF8.GetBytes(str.AsSpan(), _buffer!);
    }

    public ReadOnlySpan<byte> GetTextWritten()
        => _buffer is not null ? _buffer.WrittenSpan : default;
}
