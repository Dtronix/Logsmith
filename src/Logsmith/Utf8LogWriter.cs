using System.Buffers;
using System.Text;
using System.Text.Unicode;

namespace Logsmith;

public ref struct Utf8LogWriter
{
    private readonly Span<byte> _buffer;
    private int _position;

    public Utf8LogWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
    }

    public void Write(ReadOnlySpan<byte> utf8Literal)
    {
        if (utf8Literal.Length > _buffer.Length - _position)
            return;

        utf8Literal.CopyTo(_buffer[_position..]);
        _position += utf8Literal.Length;
    }

    public void WriteFormatted<T>(in T value) where T : IUtf8SpanFormattable
    {
        if (value.TryFormat(_buffer[_position..], out int bytesWritten, default, null))
        {
            _position += bytesWritten;
        }
    }

    public void WriteFormatted<T>(in T value, ReadOnlySpan<char> format) where T : IUtf8SpanFormattable
    {
        if (value.TryFormat(_buffer[_position..], out int bytesWritten, format, null))
        {
            _position += bytesWritten;
        }
    }

    public void WriteString(string? value)
    {
        if (value is null)
        {
            Write("null"u8);
            return;
        }

        var status = Utf8.FromUtf16(value, _buffer[_position..], out _, out int bytesWritten);
        if (status == OperationStatus.Done)
        {
            _position += bytesWritten;
        }
    }

    public ReadOnlySpan<byte> GetWritten() => _buffer[.._position];

    public int BytesWritten => _position;
}
