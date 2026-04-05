using System.Buffers;

namespace Logsmith.Formatting;

public interface ILogFormatter
{
    void FormatPrefix(in DispatchInfo info, IBufferWriter<byte> output);
    void FormatSuffix(in DispatchInfo info, IBufferWriter<byte> output);
}
