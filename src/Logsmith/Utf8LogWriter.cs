using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;

namespace Logsmith;

public ref struct Utf8LogWriter
{
    private Span<byte> _buffer;
    private int _position;
    private byte[]? _rented;

    public Utf8LogWriter(Span<byte> buffer)
    {
        _buffer = buffer;
        _position = 0;
        _rented = null;
    }

    public void Dispose()
    {
        var rented = _rented;
        if (rented is not null)
        {
            _rented = null;
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    public void Write(ReadOnlySpan<byte> utf8Literal)
    {
        if (utf8Literal.Length > _buffer.Length - _position)
            Grow(utf8Literal.Length);

        utf8Literal.CopyTo(_buffer[_position..]);
        _position += utf8Literal.Length;
    }

    public void WriteFormatted<T>(in T value) where T : IUtf8SpanFormattable
    {
        int bytesWritten;
        while (!value.TryFormat(_buffer[_position..], out bytesWritten, default, null))
            Grow(_buffer.Length);
        _position += bytesWritten;
    }

    public void WriteFormatted<T>(in T value, ReadOnlySpan<char> format) where T : IUtf8SpanFormattable
    {
        int bytesWritten;
        while (!value.TryFormat(_buffer[_position..], out bytesWritten, format, null))
            Grow(_buffer.Length);
        _position += bytesWritten;
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
            return;
        }

        // Buffer too small — grow to exact requirement and retry
        Grow(Encoding.UTF8.GetByteCount(value));
        status = Utf8.FromUtf16(value, _buffer[_position..], out _, out bytesWritten);
        if (status == OperationStatus.Done)
        {
            _position += bytesWritten;
        }
    }

    public ReadOnlySpan<byte> GetWritten() => _buffer[.._position];

    public int BytesWritten => _position;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Grow(int needed)
    {
        int required = _position + needed;
        int newSize = Math.Max(_buffer.Length * 2, required);
        var newArray = ArrayPool<byte>.Shared.Rent(newSize);
        _buffer[.._position].CopyTo(newArray);

        var old = _rented;
        _rented = newArray;
        _buffer = newArray;

        if (old is not null)
            ArrayPool<byte>.Shared.Return(old);
    }
}
