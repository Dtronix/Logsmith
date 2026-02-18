using Logsmith.Formatting;
using Logsmith.Sinks;

namespace Logsmith.Tests.SinkTests;

[TestFixture]
public class FileSinkTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"logsmith_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Test]
    public async Task Write_CreatesFileAndWritesContent()
    {
        var path = Path.Combine(_tempDir, "test.log");
        var sink = new FileSink(path);
        var entry = MakeEntry();

        sink.Write(in entry, "file test"u8);
        await sink.DisposeAsync();

        var content = await File.ReadAllTextAsync(path);
        Assert.That(content, Does.Contain("file test"));
    }

    [Test]
    public async Task Write_ExceedsMaxSize_RollsFile()
    {
        var path = Path.Combine(_tempDir, "roll.log");
        var sink = new FileSink(path, maxFileSizeBytes: 50);
        var entry = MakeEntry();

        // Write enough to exceed 50 bytes and trigger a roll
        sink.Write(in entry, "This is a message that should trigger rolling"u8);
        // Give the background drain time to process
        await Task.Delay(200);
        sink.Write(in entry, "Second message after roll"u8);
        await sink.DisposeAsync();

        var files = Directory.GetFiles(_tempDir, "roll*");
        Assert.That(files, Has.Length.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task DisposeAsync_FlushesRemainingEntries()
    {
        var path = Path.Combine(_tempDir, "flush.log");
        var sink = new FileSink(path);
        var entry = MakeEntry();

        sink.Write(in entry, "flushed"u8);
        await sink.DisposeAsync();

        var content = await File.ReadAllTextAsync(path);
        Assert.That(content, Does.Contain("flushed"));
    }

    [Test]
    public void IsEnabled_RespectsMinimumLevel()
    {
        var path = Path.Combine(_tempDir, "level.log");
        using var sink = new FileSink(path, minimumLevel: LogLevel.Error);
        Assert.That(sink.IsEnabled(LogLevel.Debug), Is.False);
        Assert.That(sink.IsEnabled(LogLevel.Error), Is.True);
    }

    [Test]
    public async Task CustomFormatter_UsedInFileOutput()
    {
        var path = Path.Combine(_tempDir, "custom.log");
        var sink = new FileSink(path, formatter: NullLogFormatter.Instance);
        var entry = MakeEntry();

        sink.Write(in entry, "raw message"u8);
        await sink.DisposeAsync();

        var content = await File.ReadAllTextAsync(path);
        // NullLogFormatter emits no prefix/suffix, so content is just the raw message
        Assert.That(content, Is.EqualTo("raw message"));
    }

    [Test]
    public void SharedMode_OpensWithReadWriteShare()
    {
        var path = Path.Combine(_tempDir, "shared.log");
        using var sink1 = new FileSink(path, shared: true, formatter: NullLogFormatter.Instance);
        // Second sink on same file should not throw
        using var sink2 = new FileSink(path, shared: true, formatter: NullLogFormatter.Instance);
        Assert.Pass();
    }

    [Test]
    public async Task SharedMode_BothProcessesWrite()
    {
        var path = Path.Combine(_tempDir, "shared_both.log");
        var sink1 = new FileSink(path, shared: true, formatter: NullLogFormatter.Instance);
        var sink2 = new FileSink(path, shared: true, formatter: NullLogFormatter.Instance);
        var entry = MakeEntry();

        sink1.Write(in entry, "from-sink1"u8);
        await Task.Delay(100);
        sink2.Write(in entry, "from-sink2"u8);
        await Task.Delay(100);
        await sink1.DisposeAsync();
        await sink2.DisposeAsync();

        var content = await File.ReadAllTextAsync(path);
        Assert.That(content, Does.Contain("from-sink1"));
        Assert.That(content, Does.Contain("from-sink2"));
    }

    [Test]
    public async Task SharedMode_MutexCoordinates_BothWritesSurvive()
    {
        var path = Path.Combine(_tempDir, "mutex_both.log");
        var sink1 = new FileSink(path, shared: true, formatter: NullLogFormatter.Instance);
        var sink2 = new FileSink(path, shared: true, formatter: NullLogFormatter.Instance);
        var entry = MakeEntry();

        // Named mutex serializes writes — both messages must survive
        sink1.Write(in entry, "from-sink1"u8);
        sink2.Write(in entry, "from-sink2"u8);
        await sink1.DisposeAsync();
        await sink2.DisposeAsync();

        var content = await File.ReadAllTextAsync(path);
        Assert.That(content, Does.Contain("from-sink1"));
        Assert.That(content, Does.Contain("from-sink2"));
    }

    [Test]
    public async Task RollingInterval_Daily_RollsOnDayBoundary()
    {
        var path = Path.Combine(_tempDir, "daily.log");
        var sink = new FileSink(path, formatter: NullLogFormatter.Instance,
            rollingInterval: RollingInterval.Daily);

        // Entry at day N (today)
        var today = DateTime.UtcNow;
        var entryToday = new LogEntry(LogLevel.Information, 1, today.Ticks, "Test");
        sink.Write(in entryToday, "day1"u8);
        await Task.Delay(100);

        // Entry at day N+1
        var tomorrow = today.AddDays(1);
        var entryTomorrow = new LogEntry(LogLevel.Information, 1, tomorrow.Ticks, "Test");
        sink.Write(in entryTomorrow, "day2"u8);
        await sink.DisposeAsync();

        var files = Directory.GetFiles(_tempDir, "daily*");
        Assert.That(files, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task RollingInterval_Hourly_RollsOnHourBoundary()
    {
        var path = Path.Combine(_tempDir, "hourly.log");
        var sink = new FileSink(path, formatter: NullLogFormatter.Instance,
            rollingInterval: RollingInterval.Hourly);

        // Entry at hour H (now truncated to current hour)
        var now = DateTime.UtcNow;
        var thisHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 30, 0, DateTimeKind.Utc);
        var entryH = new LogEntry(LogLevel.Information, 1, thisHour.Ticks, "Test");
        sink.Write(in entryH, "hour1"u8);
        await Task.Delay(100);

        // Entry at hour H+1
        var nextHour = thisHour.AddHours(1);
        var entryH1 = new LogEntry(LogLevel.Information, 1, nextHour.Ticks, "Test");
        sink.Write(in entryH1, "hour2"u8);
        await sink.DisposeAsync();

        var files = Directory.GetFiles(_tempDir, "hourly*");
        Assert.That(files, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task RollingInterval_None_NoTimeRoll()
    {
        var path = Path.Combine(_tempDir, "noroll.log");
        var sink = new FileSink(path, formatter: NullLogFormatter.Instance,
            rollingInterval: RollingInterval.None);

        var now = DateTime.UtcNow;
        var entry1 = new LogEntry(LogLevel.Information, 1, now.Ticks, "Test");
        sink.Write(in entry1, "first"u8);
        await Task.Delay(100);

        // 2 hours later — should NOT roll
        var later = now.AddHours(2);
        var entry2 = new LogEntry(LogLevel.Information, 1, later.Ticks, "Test");
        sink.Write(in entry2, "second"u8);
        await sink.DisposeAsync();

        var files = Directory.GetFiles(_tempDir, "noroll*");
        Assert.That(files, Has.Length.EqualTo(1));

        var content = await File.ReadAllTextAsync(path);
        Assert.That(content, Does.Contain("first"));
        Assert.That(content, Does.Contain("second"));
    }

    [Test]
    public async Task RollingInterval_Daily_FileNaming()
    {
        var path = Path.Combine(_tempDir, "app.log");
        var sink = new FileSink(path, formatter: NullLogFormatter.Instance,
            rollingInterval: RollingInterval.Daily);

        var today = DateTime.UtcNow;
        var entryToday = new LogEntry(LogLevel.Information, 1, today.Ticks, "Test");
        sink.Write(in entryToday, "today"u8);
        await Task.Delay(100);

        var tomorrow = today.AddDays(1);
        var entryTomorrow = new LogEntry(LogLevel.Information, 1, tomorrow.Ticks, "Test");
        sink.Write(in entryTomorrow, "tomorrow"u8);
        await sink.DisposeAsync();

        // The rolled file should match pattern app.yyyy-MM-dd.log
        var files = Directory.GetFiles(_tempDir, "app.????-??-??.log");
        Assert.That(files, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RollingInterval_Hourly_FileNaming()
    {
        var path = Path.Combine(_tempDir, "app.log");
        var sink = new FileSink(path, formatter: NullLogFormatter.Instance,
            rollingInterval: RollingInterval.Hourly);

        var now = DateTime.UtcNow;
        var thisHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 30, 0, DateTimeKind.Utc);
        var entryH = new LogEntry(LogLevel.Information, 1, thisHour.Ticks, "Test");
        sink.Write(in entryH, "hourA"u8);
        await Task.Delay(100);

        var nextHour = thisHour.AddHours(1);
        var entryH1 = new LogEntry(LogLevel.Information, 1, nextHour.Ticks, "Test");
        sink.Write(in entryH1, "hourB"u8);
        await sink.DisposeAsync();

        // The rolled file should match pattern app.yyyy-MM-dd-HH.log
        var files = Directory.GetFiles(_tempDir, "app.????-??-??-??.log");
        Assert.That(files, Has.Length.EqualTo(1));
    }

    [Test]
    public async Task RollingInterval_SizeRollWithinPeriod_SequenceNumber()
    {
        var path = Path.Combine(_tempDir, "seq.log");
        var sink = new FileSink(path, formatter: NullLogFormatter.Instance,
            rollingInterval: RollingInterval.Daily, maxFileSizeBytes: 20);

        var now = DateTime.UtcNow;

        // Write enough to trigger multiple size rolls within same day
        for (int i = 0; i < 3; i++)
        {
            var entry = new LogEntry(LogLevel.Information, 1, now.Ticks, "Test");
            sink.Write(in entry, "this-is-a-long-message-for-rolling"u8);
            await Task.Delay(150);
        }

        await sink.DisposeAsync();

        // Should have rolled files with sequence numbers
        var files = Directory.GetFiles(_tempDir, "seq*");
        Assert.That(files, Has.Length.GreaterThanOrEqualTo(2));
    }

    private static LogEntry MakeEntry() => new(
        LogLevel.Information, 1, DateTime.UtcNow.Ticks, "Test");
}
