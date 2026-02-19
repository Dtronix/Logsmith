using System.Buffers;

namespace Logsmith.Internal;

internal static class ThreadBuffer
{
    [ThreadStatic] private static ArrayBufferWriter<byte>? _buffer;

    internal static ArrayBufferWriter<byte> Get()
    {
        var buffer = _buffer ??= new ArrayBufferWriter<byte>(512);
        buffer.ResetWrittenCount();
        return buffer;
    }
}
