using System.Buffers;
using System.Text.Json;

namespace Logsmith.Internal;

internal static class ThreadBuffer
{
    [ThreadStatic] private static ArrayBufferWriter<byte>? _buffer;
    [ThreadStatic] private static ArrayBufferWriter<byte>? _handlerTextBuffer;
    [ThreadStatic] private static ArrayBufferWriter<byte>? _handlerJsonBuffer;
    [ThreadStatic] private static Utf8JsonWriter? _jsonWriter;

    /// <summary>
    /// Returns the thread-local sink formatting buffer, reset for reuse.
    /// Used by sinks during dispatch — separate from handler buffers.
    /// </summary>
    internal static ArrayBufferWriter<byte> Get()
    {
        var buffer = _buffer ??= new ArrayBufferWriter<byte>(512);
        buffer.ResetWrittenCount();
        return buffer;
    }

    /// <summary>
    /// Returns the thread-local handler text buffer, reset for reuse.
    /// Used by LogHandlerCore for UTF-8 message text.
    /// </summary>
    internal static ArrayBufferWriter<byte> GetHandlerText()
    {
        var buffer = _handlerTextBuffer ??= new ArrayBufferWriter<byte>(512);
        buffer.ResetWrittenCount();
        return buffer;
    }

    /// <summary>
    /// Returns the thread-local handler JSON buffer, reset for reuse.
    /// Used by LogHandlerCore for structured JSON properties.
    /// </summary>
    internal static ArrayBufferWriter<byte> GetHandlerJson()
    {
        var buffer = _handlerJsonBuffer ??= new ArrayBufferWriter<byte>(512);
        buffer.ResetWrittenCount();
        return buffer;
    }

    /// <summary>
    /// Returns the thread-local Utf8JsonWriter, reset to write to the given output.
    /// The writer retains its internal rented arrays across calls, avoiding
    /// GC finalization overhead from undisposed writers.
    /// </summary>
    internal static Utf8JsonWriter GetJsonWriter(IBufferWriter<byte> output)
    {
        var writer = _jsonWriter;
        if (writer is null)
        {
            writer = new Utf8JsonWriter(output);
            _jsonWriter = writer;
        }
        else
        {
            writer.Reset(output);
        }

        return writer;
    }
}
