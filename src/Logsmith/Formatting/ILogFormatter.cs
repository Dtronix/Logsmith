using System.Buffers;

namespace Logsmith.Formatting;

public interface ILogFormatter
{
    void FormatPrefix(in LogEntry entry, IBufferWriter<byte> output);
    void FormatSuffix(in LogEntry entry, IBufferWriter<byte> output);
}
