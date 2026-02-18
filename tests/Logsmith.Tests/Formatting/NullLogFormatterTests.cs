using System.Buffers;
using Logsmith.Formatting;

namespace Logsmith.Tests.Formatting;

[TestFixture]
public class NullLogFormatterTests
{
    [Test]
    public void FormatPrefix_WritesNothing()
    {
        var buffer = new ArrayBufferWriter<byte>(64);
        var entry = new LogEntry(LogLevel.Information, 1, DateTime.UtcNow.Ticks, "Test");

        NullLogFormatter.Instance.FormatPrefix(in entry, buffer);

        Assert.That(buffer.WrittenCount, Is.EqualTo(0));
    }

    [Test]
    public void FormatSuffix_WritesNothing()
    {
        var buffer = new ArrayBufferWriter<byte>(64);
        var entry = new LogEntry(LogLevel.Information, 1, DateTime.UtcNow.Ticks, "Test");

        NullLogFormatter.Instance.FormatSuffix(in entry, buffer);

        Assert.That(buffer.WrittenCount, Is.EqualTo(0));
    }
}
