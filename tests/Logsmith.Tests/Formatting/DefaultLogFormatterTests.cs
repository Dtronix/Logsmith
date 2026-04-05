using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Logsmith.Formatting;

namespace Logsmith.Tests.Formatting;

[TestFixture]
public class DefaultLogFormatterTests
{
    [Test]
    public void FormatPrefix_TimeOnly_MatchesConsoleFormat()
    {
        var formatter = new DefaultLogFormatter(includeDate: false);
        var info = MakeInfo(LogLevel.Information);
        var buffer = new ArrayBufferWriter<byte>(256);

        formatter.FormatPrefix(in info, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        // Matches [HH:mm:ss.fff INF Test]
        Assert.That(result, Does.Match(@"^\[\d{2}:\d{2}:\d{2}\.\d{3} INF Test\] $"));
    }

    [Test]
    public void FormatPrefix_WithDate_MatchesFileFormat()
    {
        var formatter = new DefaultLogFormatter(includeDate: true);
        var info = MakeInfo(LogLevel.Information);
        var buffer = new ArrayBufferWriter<byte>(256);

        formatter.FormatPrefix(in info, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        // Matches [yyyy-MM-dd HH:mm:ss.fff INF Test]
        Assert.That(result, Does.Match(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} INF Test\] $"));
    }

    [Test]
    public void FormatSuffix_WritesNewline()
    {
        var formatter = new DefaultLogFormatter();
        var info = MakeInfo(LogLevel.Information);
        var buffer = new ArrayBufferWriter<byte>(64);

        formatter.FormatSuffix(in info, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        Assert.That(result, Is.EqualTo("\n"));
    }

    [Test]
    public void FormatSuffix_WithException_WritesExceptionAfterNewline()
    {
        var formatter = new DefaultLogFormatter();
        var ex = new InvalidOperationException("test error");
        var info = new DispatchInfo
        {
            Level = LogLevel.Error,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
            Exception = ex,
        };
        var buffer = new ArrayBufferWriter<byte>(4096);

        formatter.FormatSuffix(in info, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        Assert.That(result, Does.StartWith("\n"));
        Assert.That(result, Does.Contain("test error"));
        Assert.That(result, Does.Contain("InvalidOperationException"));
    }

    [TestCase(LogLevel.Trace, "TRC")]
    [TestCase(LogLevel.Debug, "DBG")]
    [TestCase(LogLevel.Information, "INF")]
    [TestCase(LogLevel.Warning, "WRN")]
    [TestCase(LogLevel.Error, "ERR")]
    [TestCase(LogLevel.Critical, "CRT")]
    public void FormatPrefix_AllLevels_CorrectTags(LogLevel level, string expectedTag)
    {
        var formatter = new DefaultLogFormatter();
        var info = MakeInfo(level);
        var buffer = new ArrayBufferWriter<byte>(256);

        formatter.FormatPrefix(in info, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        Assert.That(result, Does.Contain(expectedTag));
    }

    private static DispatchInfo MakeInfo(LogLevel level) => new()
    {
        Level = level,
        EventId = 1,
        TimestampTicks = DateTime.UtcNow.Ticks,
        Category = "Test",
    };
}
