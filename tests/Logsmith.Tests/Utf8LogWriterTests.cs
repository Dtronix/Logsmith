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
        writer.Dispose();
    }

    [Test]
    public void WriteFormatted_WithEmptyFormat_SameAsDefault()
    {
        Span<byte> buffer = stackalloc byte[128];
        var writer = new Utf8LogWriter(buffer);

        writer.WriteFormatted(42, "");

        var result = Encoding.UTF8.GetString(writer.GetWritten());
        Assert.That(result, Is.EqualTo("42"));
        writer.Dispose();
    }

    [Test]
    public void WriteString_WithCurlyBraces_PreservesBraces()
    {
        Span<byte> buffer = stackalloc byte[512];
        var writer = new Utf8LogWriter(buffer);

        writer.Write("Vulkan validation: "u8);
        writer.WriteString("{VkBuffer 0x00000001234ABCDEF} - Memory type not suitable");

        var result = Encoding.UTF8.GetString(writer.GetWritten());
        Assert.That(result, Is.EqualTo("Vulkan validation: {VkBuffer 0x00000001234ABCDEF} - Memory type not suitable"));
        writer.Dispose();
    }

    [Test]
    public void WriteString_OverflowBuffer_FallsBackToArrayPool()
    {
        // Buffer only fits the literal prefix, not the string param
        Span<byte> buffer = stackalloc byte[30];
        var writer = new Utf8LogWriter(buffer);

        writer.Write("Prefix: "u8); // 8 bytes, leaves 22 bytes
        writer.WriteString("{VkBuffer 0x00000001234ABCDEF} - Memory type not suitable"); // 57 bytes — overflows

        var result = Encoding.UTF8.GetString(writer.GetWritten());
        Assert.That(result, Is.EqualTo("Prefix: {VkBuffer 0x00000001234ABCDEF} - Memory type not suitable"));
        writer.Dispose();
    }

    [Test]
    public void WriteString_LongMessage_PreservedViaArrayPoolFallback()
    {
        // Max generated buffer is 4096 bytes. A single string param can easily exceed that —
        // stack traces, serialized payloads, validation messages, SQL queries, etc.
        const int maxGeneratedBuffer = 4096;
        Span<byte> buffer = stackalloc byte[maxGeneratedBuffer];
        var writer = new Utf8LogWriter(buffer);

        writer.Write("Payload: "u8); // 9 bytes

        // 8KB string — double the max buffer. Not unusual for serialized objects,
        // full stack traces, or verbose diagnostic output.
        var longMessage = new string('X', 8192);

        writer.WriteString(longMessage);

        var result = Encoding.UTF8.GetString(writer.GetWritten());
        Assert.That(result, Is.EqualTo("Payload: " + longMessage));
        Assert.That(writer.BytesWritten, Is.EqualTo(9 + 8192));
        writer.Dispose();
    }

    [Test]
    public void Write_LiteralOverflow_PreservedViaArrayPoolFallback()
    {
        Span<byte> buffer = stackalloc byte[4];
        var writer = new Utf8LogWriter(buffer);

        writer.Write("Hello, world!"u8); // 13 bytes into 4-byte buffer

        var result = Encoding.UTF8.GetString(writer.GetWritten());
        Assert.That(result, Is.EqualTo("Hello, world!"));
        writer.Dispose();
    }

    [Test]
    public void WriteFormatted_Overflow_PreservedViaArrayPoolFallback()
    {
        Span<byte> buffer = stackalloc byte[4];
        var writer = new Utf8LogWriter(buffer);

        writer.WriteFormatted(123456789L); // needs ~9 bytes, only 4 available

        var result = Encoding.UTF8.GetString(writer.GetWritten());
        Assert.That(result, Is.EqualTo("123456789"));
        writer.Dispose();
    }

    [Test]
    public void Dispose_NoOverflow_NoOp()
    {
        Span<byte> buffer = stackalloc byte[128];
        var writer = new Utf8LogWriter(buffer);

        writer.Write("hello"u8);

        // Dispose on stackalloc-only path should be a no-op (no rented buffer to return)
        writer.Dispose();
        Assert.Pass();
    }
}
