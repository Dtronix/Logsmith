using System.Buffers;
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
}
