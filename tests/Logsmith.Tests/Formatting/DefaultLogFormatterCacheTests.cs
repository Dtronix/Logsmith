using System.Buffers;
using System.Text;
using Logsmith.Formatting;

namespace Logsmith.Tests.Formatting;

[TestFixture]
public class DefaultLogFormatterCacheTests
{
    [Test]
    public void FormatPrefix_UnicodeCategory_EncodesCorrectly()
    {
        var formatter = new DefaultLogFormatter(includeDate: false);
        var info = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "\u65E5\u672C\u8A9E\u30AB\u30C6\u30B4\u30EA",
        };
        var buffer = new ArrayBufferWriter<byte>(512);

        formatter.FormatPrefix(in info, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        Assert.That(result, Does.Contain("\u65E5\u672C\u8A9E\u30AB\u30C6\u30B4\u30EA"));
    }

    [Test]
    public void FormatPrefix_RepeatedCategory_ProducesIdenticalOutput()
    {
        var formatter = new DefaultLogFormatter(includeDate: false);
        var ticks = DateTime.UtcNow.Ticks;

        var info1 = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = ticks,
            Category = "Repeated",
        };
        var buf1 = new ArrayBufferWriter<byte>(256);
        formatter.FormatPrefix(in info1, buf1);

        var info2 = new DispatchInfo
        {
            Level = LogLevel.Information,
            EventId = 1,
            TimestampTicks = ticks,
            Category = "Repeated",
        };
        var buf2 = new ArrayBufferWriter<byte>(256);
        formatter.FormatPrefix(in info2, buf2);

        Assert.That(buf1.WrittenSpan.SequenceEqual(buf2.WrittenSpan), Is.True);
    }

    [Test]
    public void FormatPrefix_MultipleDifferentCategories_AllCorrect()
    {
        var formatter = new DefaultLogFormatter(includeDate: false);
        var categories = new[] { "Alpha", "Bravo", "Charlie", "Delta" };

        foreach (var cat in categories)
        {
            var info = new DispatchInfo
            {
                Level = LogLevel.Information,
                EventId = 1,
                TimestampTicks = DateTime.UtcNow.Ticks,
                Category = cat,
            };
            var buf = new ArrayBufferWriter<byte>(256);
            formatter.FormatPrefix(in info, buf);
            var result = Encoding.UTF8.GetString(buf.WrittenSpan);

            Assert.That(result, Does.Contain(cat), $"Category '{cat}' not found in prefix");
        }
    }

    [Test]
    public void FormatSuffix_LargeNestedException_EncodesCompletely()
    {
        var formatter = new DefaultLogFormatter();
        Exception nested;
        try
        {
            try
            {
                try
                {
                    throw new InvalidOperationException("inner " + new string('x', 2000));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("middle " + new string('y', 2000), ex);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("outer " + new string('z', 2000), ex);
            }
        }
        catch (Exception ex)
        {
            nested = ex;
        }

        var info = new DispatchInfo
        {
            Level = LogLevel.Error,
            EventId = 1,
            TimestampTicks = DateTime.UtcNow.Ticks,
            Category = "Test",
            Exception = nested,
        };
        var buffer = new ArrayBufferWriter<byte>(16384);

        formatter.FormatSuffix(in info, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        Assert.That(result, Does.Contain("outer"));
        Assert.That(result, Does.Contain("middle"));
        Assert.That(result, Does.Contain("inner"));
        Assert.That(result, Does.Contain("ApplicationException"));
        Assert.That(result, Does.Contain("ArgumentException"));
        Assert.That(result, Does.Contain("InvalidOperationException"));
    }

    [Test]
    public void FormatSuffix_ExceptionWithUnicode_EncodesCorrectly()
    {
        var formatter = new DefaultLogFormatter();
        var ex = new InvalidOperationException("\u30A8\u30E9\u30FC\u304C\u767A\u751F\u3057\u307E\u3057\u305F: \U0001F525");
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

        Assert.That(result, Does.Contain("\u30A8\u30E9\u30FC\u304C\u767A\u751F\u3057\u307E\u3057\u305F"));
        Assert.That(result, Does.Contain("\U0001F525"));
    }
}
