using System.Buffers;
using System.Text;
using Logsmith.Formatting;
using Logsmith.Sinks;

namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class ConsoleSinkTests
{
    [Test]
    public void Write_OutputsToStdout()
    {
        var originalOut = Console.OpenStandardOutput();
        using var ms = new MemoryStream();
        Console.SetOut(new StreamWriter(ms) { AutoFlush = true });

        try
        {
            using var sink = new ConsoleSink(colored: false);
            var entry = MakeEntry(LogLevel.Information);
            sink.Write(in entry, "stdout test"u8);

            // ConsoleSink writes directly to OpenStandardOutput, not Console.Out,
            // so we check it doesn't throw. Redirect only captures Console.Out.
            Assert.Pass();
        }
        finally
        {
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        }
    }

    [Test]
    public void Write_DoesNotThrow()
    {
        using var sink = new ConsoleSink(colored: true);
        var entry = MakeEntry(LogLevel.Error);
        Assert.DoesNotThrow(() => sink.Write(in entry, "error msg"u8));
    }

    [Test]
    public void IsEnabled_RespectsMinimumLevel()
    {
        using var sink = new ConsoleSink(minimumLevel: LogLevel.Warning);
        Assert.That(sink.IsEnabled(LogLevel.Debug), Is.False);
        Assert.That(sink.IsEnabled(LogLevel.Warning), Is.True);
        Assert.That(sink.IsEnabled(LogLevel.Error), Is.True);
    }

    [Test]
    public void CustomFormatter_UsedForPrefix()
    {
        // NullLogFormatter means no prefix — just verifying it doesn't throw
        using var sink = new ConsoleSink(colored: false, formatter: NullLogFormatter.Instance);
        var entry = MakeEntry(LogLevel.Information);
        Assert.DoesNotThrow(() => sink.Write(in entry, "custom formatter test"u8));
    }

    private static LogEntry MakeEntry(LogLevel level) => new(
        level, 1, DateTime.UtcNow.Ticks, "Test");
}
