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
        var entry = new LogEntry(LogLevel.Information, 1, DateTime.UtcNow.Ticks, "日本語カテゴリ");
        var buffer = new ArrayBufferWriter<byte>(512);

        formatter.FormatPrefix(in entry, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        Assert.That(result, Does.Contain("日本語カテゴリ"));
    }

    [Test]
    public void FormatPrefix_RepeatedCategory_ProducesIdenticalOutput()
    {
        var formatter = new DefaultLogFormatter(includeDate: false);
        var ticks = DateTime.UtcNow.Ticks;

        var entry1 = new LogEntry(LogLevel.Information, 1, ticks, "Repeated");
        var buf1 = new ArrayBufferWriter<byte>(256);
        formatter.FormatPrefix(in entry1, buf1);

        var entry2 = new LogEntry(LogLevel.Information, 1, ticks, "Repeated");
        var buf2 = new ArrayBufferWriter<byte>(256);
        formatter.FormatPrefix(in entry2, buf2);

        Assert.That(buf1.WrittenSpan.SequenceEqual(buf2.WrittenSpan), Is.True);
    }

    [Test]
    public void FormatPrefix_MultipleDifferentCategories_AllCorrect()
    {
        var formatter = new DefaultLogFormatter(includeDate: false);
        var categories = new[] { "Alpha", "Bravo", "Charlie", "Delta" };

        foreach (var cat in categories)
        {
            var entry = new LogEntry(LogLevel.Information, 1, DateTime.UtcNow.Ticks, cat);
            var buf = new ArrayBufferWriter<byte>(256);
            formatter.FormatPrefix(in entry, buf);
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

        var entry = new LogEntry(LogLevel.Error, 1, DateTime.UtcNow.Ticks, "Test", exception: nested);
        var buffer = new ArrayBufferWriter<byte>(16384);

        formatter.FormatSuffix(in entry, buffer);
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
        var ex = new InvalidOperationException("エラーが発生しました: 🔥");
        var entry = new LogEntry(LogLevel.Error, 1, DateTime.UtcNow.Ticks, "Test", exception: ex);
        var buffer = new ArrayBufferWriter<byte>(4096);

        formatter.FormatSuffix(in entry, buffer);
        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        Assert.That(result, Does.Contain("エラーが発生しました"));
        Assert.That(result, Does.Contain("🔥"));
    }
}
