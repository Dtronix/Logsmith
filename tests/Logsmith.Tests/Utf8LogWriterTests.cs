using System.Text;

namespace Logsmith.Tests;

[TestFixture]
public class Utf8LogWriterTests
{
    [Test]
    public void WriteFormatted_WithFormat_AppliesFormatString()
    {
        Span<byte> buffer = stackalloc byte[128];
        var writer = new Utf8LogWriter(buffer);

        writer.WriteFormatted(3.14159, "F2");

        var result = Encoding.UTF8.GetString(writer.GetWritten());
        Assert.That(result, Is.EqualTo("3.14"));
    }

    [Test]
    public void WriteFormatted_WithEmptyFormat_SameAsDefault()
    {
        Span<byte> buffer = stackalloc byte[128];
        var writer = new Utf8LogWriter(buffer);

        writer.WriteFormatted(42, "");

        var result = Encoding.UTF8.GetString(writer.GetWritten());
        Assert.That(result, Is.EqualTo("42"));
    }
}
