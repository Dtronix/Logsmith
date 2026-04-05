using System.Buffers;

namespace Logsmith.Formatting;

public sealed class NullLogFormatter : ILogFormatter
{
    public static readonly NullLogFormatter Instance = new();
    public void FormatPrefix(in DispatchInfo info, IBufferWriter<byte> output) { }
    public void FormatSuffix(in DispatchInfo info, IBufferWriter<byte> output) { }
}
