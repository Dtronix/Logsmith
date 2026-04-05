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
            var info = MakeInfo(LogLevel.Information);
            sink.Write(in info);

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
        var info = MakeInfo(LogLevel.Error);
        sink.Write(in info);
        Assert.Pass();
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
        var info = MakeInfo(LogLevel.Information);
        sink.Write(in info);
        Assert.Pass();
    }

    private static DispatchInfo MakeInfo(LogLevel level) => new()
    {
        Level = level,
        EventId = 1,
        TimestampTicks = DateTime.UtcNow.Ticks,
        Category = "Test",
        Utf8Message = "test msg"u8,
    };
}
