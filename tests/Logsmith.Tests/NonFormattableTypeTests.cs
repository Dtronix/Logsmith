using Logsmith.Sinks;

namespace Logsmith.Tests;

public enum TestColor { Red, Green, Blue }

[LogCategory("TypeTests")]
public static partial class TypeTestLog
{
    [LogMessage(LogLevel.Information, "Flag={flag}")]
    public static partial void LogBool(bool flag);

    [LogMessage(LogLevel.Information, "Flag={flag}")]
    public static partial void LogNullableBool(bool? flag);

    [LogMessage(LogLevel.Information, "Color={color}")]
    public static partial void LogEnum(TestColor color);

    [LogMessage(LogLevel.Information, "Color={color}")]
    public static partial void LogNullableEnum(TestColor? color);

    [LogMessage(LogLevel.Information, "Count={count}")]
    public static partial void LogInt(int count);

    [LogMessage(LogLevel.Information, "Price={price:F2}")]
    public static partial void LogDecimal(decimal price);

    [LogMessage(LogLevel.Information, "Flag={flag} Count={count} Color={color}")]
    public static partial void LogMixed(bool flag, int count, TestColor color);
}

[TestFixture]
public class NonFormattableTypeTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown() => LogManager.Reset();

    [Test]
    public void Bool_True_WritesTrue()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogBool(true);

        Assert.That(sink.Entries, Has.Count.EqualTo(1));
        Assert.That(sink.Entries[0].Message, Does.Contain("Flag=true"));
    }

    [Test]
    public void Bool_False_WritesFalse()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogBool(false);

        Assert.That(sink.Entries[0].Message, Does.Contain("Flag=false"));
    }

    [Test]
    public void NullableBool_WithValue_WritesValue()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogNullableBool(true);

        Assert.That(sink.Entries[0].Message, Does.Contain("Flag=true"));
    }

    [Test]
    public void NullableBool_Null_WritesNull()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogNullableBool(null);

        Assert.That(sink.Entries[0].Message, Does.Contain("Flag=null"));
    }

    [Test]
    public void Enum_WritesName()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogEnum(TestColor.Green);

        Assert.That(sink.Entries[0].Message, Does.Contain("Color=Green"));
    }

    [Test]
    public void NullableEnum_WithValue_WritesName()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogNullableEnum(TestColor.Blue);

        Assert.That(sink.Entries[0].Message, Does.Contain("Color=Blue"));
    }

    [Test]
    public void NullableEnum_Null_WritesNull()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogNullableEnum(null);

        Assert.That(sink.Entries[0].Message, Does.Contain("Color=null"));
    }

    [Test]
    public void Int_StillWorks()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogInt(42);

        Assert.That(sink.Entries[0].Message, Does.Contain("Count=42"));
    }

    [Test]
    public void Decimal_WithFormatSpecifier_Formats()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogDecimal(3.14159m);

        Assert.That(sink.Entries[0].Message, Does.Contain("Price=3.14"));
    }

    [Test]
    public void MixedTypes_AllFormatCorrectly()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c => c.AddSink(sink));

        TypeTestLog.LogMixed(true, 7, TestColor.Red);

        var msg = sink.Entries[0].Message;
        Assert.That(msg, Does.Contain("Flag=true"));
        Assert.That(msg, Does.Contain("Count=7"));
        Assert.That(msg, Does.Contain("Color=Red"));
    }
}
