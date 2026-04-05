using System.Buffers;
using System.Text.Json;
using Logsmith.Internal;

namespace Logsmith.Tests.Internal;

[TestFixture]
public class ThreadBufferTests
{
    [Test]
    public void Get_ReturnsSameInstance_OnSameThread()
    {
        var first = ThreadBuffer.Get();
        var second = ThreadBuffer.Get();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void Get_ResetsWrittenCount()
    {
        var buf = ThreadBuffer.Get();
        buf.Write("hello"u8);
        Assert.That(buf.WrittenCount, Is.EqualTo(5));

        var buf2 = ThreadBuffer.Get();
        Assert.That(buf2.WrittenCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Get_ReturnsDifferentInstance_OnDifferentThread()
    {
        var mainBuffer = ThreadBuffer.Get();
        ArrayBufferWriter<byte>? otherBuffer = null;

        await Task.Run(() => otherBuffer = ThreadBuffer.Get());

        Assert.That(otherBuffer, Is.Not.SameAs(mainBuffer));
    }

    [Test]
    public void Get_IsUsable_AfterReset()
    {
        var buf = ThreadBuffer.Get();
        buf.Write("first"u8);

        buf = ThreadBuffer.Get();
        buf.Write("second"u8);

        Assert.That(buf.WrittenCount, Is.EqualTo(6));
        Assert.That(buf.WrittenSpan.SequenceEqual("second"u8), Is.True);
    }

    [Test]
    public void GetHandlerText_ReturnsSameInstance_OnSameThread()
    {
        var first = ThreadBuffer.GetHandlerText();
        var second = ThreadBuffer.GetHandlerText();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void GetHandlerText_ResetsWrittenCount()
    {
        var buf = ThreadBuffer.GetHandlerText();
        buf.Write("hello"u8);
        Assert.That(buf.WrittenCount, Is.EqualTo(5));

        var buf2 = ThreadBuffer.GetHandlerText();
        Assert.That(buf2.WrittenCount, Is.EqualTo(0));
    }

    [Test]
    public void GetHandlerText_IsDifferentFrom_Get()
    {
        var sinkBuffer = ThreadBuffer.Get();
        var handlerTextBuffer = ThreadBuffer.GetHandlerText();

        Assert.That(handlerTextBuffer, Is.Not.SameAs(sinkBuffer));
    }

    [Test]
    public void GetHandlerJson_ReturnsSameInstance_OnSameThread()
    {
        var first = ThreadBuffer.GetHandlerJson();
        var second = ThreadBuffer.GetHandlerJson();

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void GetHandlerJson_ResetsWrittenCount()
    {
        var buf = ThreadBuffer.GetHandlerJson();
        buf.Write("data"u8);
        Assert.That(buf.WrittenCount, Is.EqualTo(4));

        var buf2 = ThreadBuffer.GetHandlerJson();
        Assert.That(buf2.WrittenCount, Is.EqualTo(0));
    }

    [Test]
    public void GetHandlerJson_IsDifferentFrom_GetAndGetHandlerText()
    {
        var sinkBuffer = ThreadBuffer.Get();
        var textBuffer = ThreadBuffer.GetHandlerText();
        var jsonBuffer = ThreadBuffer.GetHandlerJson();

        Assert.That(jsonBuffer, Is.Not.SameAs(sinkBuffer));
        Assert.That(jsonBuffer, Is.Not.SameAs(textBuffer));
    }

    [Test]
    public void GetJsonWriter_ReturnsSameInstance_OnSameThread()
    {
        var jsonBuffer = ThreadBuffer.GetHandlerJson();
        var first = ThreadBuffer.GetJsonWriter(jsonBuffer);
        var second = ThreadBuffer.GetJsonWriter(jsonBuffer);

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void GetJsonWriter_ResetsToNewOutput_ProducesValidJson()
    {
        var buf1 = new ArrayBufferWriter<byte>(64);
        var writer = ThreadBuffer.GetJsonWriter(buf1);
        writer.WriteStartObject();
        writer.WriteNumber("a", 1);
        writer.WriteEndObject();
        writer.Flush();

        var json1 = JsonDocument.Parse(buf1.WrittenSpan.ToArray());
        Assert.That(json1.RootElement.GetProperty("a").GetInt32(), Is.EqualTo(1));

        var buf2 = new ArrayBufferWriter<byte>(64);
        var writer2 = ThreadBuffer.GetJsonWriter(buf2);

        Assert.That(writer2, Is.SameAs(writer));
        writer2.WriteStartObject();
        writer2.WriteNumber("b", 2);
        writer2.WriteEndObject();
        writer2.Flush();

        var json2 = JsonDocument.Parse(buf2.WrittenSpan.ToArray());
        Assert.That(json2.RootElement.GetProperty("b").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task GetHandlerText_ReturnsDifferentInstance_OnDifferentThread()
    {
        var mainBuffer = ThreadBuffer.GetHandlerText();
        ArrayBufferWriter<byte>? otherBuffer = null;

        await Task.Run(() => otherBuffer = ThreadBuffer.GetHandlerText());

        Assert.That(otherBuffer, Is.Not.SameAs(mainBuffer));
    }

    [Test]
    public async Task GetHandlerJson_ReturnsDifferentInstance_OnDifferentThread()
    {
        var mainBuffer = ThreadBuffer.GetHandlerJson();
        ArrayBufferWriter<byte>? otherBuffer = null;

        await Task.Run(() => otherBuffer = ThreadBuffer.GetHandlerJson());

        Assert.That(otherBuffer, Is.Not.SameAs(mainBuffer));
    }

    [Test]
    public async Task GetJsonWriter_ReturnsDifferentInstance_OnDifferentThread()
    {
        var buf = ThreadBuffer.GetHandlerJson();
        var mainWriter = ThreadBuffer.GetJsonWriter(buf);
        Utf8JsonWriter? otherWriter = null;

        await Task.Run(() =>
        {
            var otherBuf = ThreadBuffer.GetHandlerJson();
            otherWriter = ThreadBuffer.GetJsonWriter(otherBuf);
        });

        Assert.That(otherWriter, Is.Not.SameAs(mainWriter));
    }
}
