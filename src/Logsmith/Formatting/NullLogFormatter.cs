using System.Buffers;

namespace Logsmith.Formatting;

public sealed class NullLogFormatter : ILogFormatter
{
    public static readonly NullLogFormatter Instance = new();
    public void FormatPrefix(in LogEntry entry, IBufferWriter<byte> output) { }
    public void FormatSuffix(in LogEntry entry, IBufferWriter<byte> output) { }
}
