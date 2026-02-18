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
        sink2.Write(in entry, "from-sink2"u8);
        await sink1.DisposeAsync();
        await sink2.DisposeAsync();

        var content = await File.ReadAllTextAsync(path);
        Assert.That(content, Does.Contain("from-sink1"));
        Assert.That(content, Does.Contain("from-sink2"));
    }

    [Test]
    public void NonSharedMode_DefaultBehavior_Unchanged()
    {
        var path = Path.Combine(_tempDir, "nonshared.log");
        using var sink = new FileSink(path, shared: false, formatter: NullLogFormatter.Instance);
        // Second sink on same file with non-shared mode should throw
        Assert.Throws<IOException>(() =>
        {
            using var sink2 = new FileSink(path, shared: false, formatter: NullLogFormatter.Instance);
        });
    }

    private static LogEntry MakeEntry() => new(
        LogLevel.Information, 1, DateTime.UtcNow.Ticks, "Test");
}
