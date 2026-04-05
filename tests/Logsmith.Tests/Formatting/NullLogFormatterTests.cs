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
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
        };

        NullLogFormatter.Instance.FormatPrefix(in info, buffer);

        Assert.That(buffer.WrittenCount, Is.EqualTo(0));
    }

    [Test]
    public void FormatSuffix_WritesNothing()
    {
        var buffer = new ArrayBufferWriter<byte>(64);
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
        };

        NullLogFormatter.Instance.FormatSuffix(in info, buffer);

        Assert.That(buffer.WrittenCount, Is.EqualTo(0));
    }
}
