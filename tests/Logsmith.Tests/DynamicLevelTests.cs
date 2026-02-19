using System.Text;
using Logsmith.Sinks;

namespace Logsmith.Tests;

[TestFixture]
public class DynamicLevelTests
{
    [SetUp]
    public void SetUp() => LogManager.Reset();

    [TearDown]
    public void TearDown()
    {
        LogManager.Reset();
        Environment.SetEnvironmentVariable("TEST_LOG_LEVEL", null);
    }

    [Test]
    public void SetMinimumLevel_ChangesFilteringDynamically()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        DispatchTestMessage(LogLevel.Debug, "before");
        Assert.That(sink.Entries, Has.Count.EqualTo(1));

        LogManager.SetMinimumLevel(LogLevel.Warning);

        DispatchTestMessage(LogLevel.Debug, "filtered");
        Assert.That(sink.Entries, Has.Count.EqualTo(1));

        DispatchTestMessage(LogLevel.Warning, "after");
        Assert.That(sink.Entries, Has.Count.EqualTo(2));
    }

    [Test]
    public void SetCategoryOverrides_ChangesFilteringDynamically()
    {
        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        DispatchTestMessage(LogLevel.Debug, "before", "Noisy");
        Assert.That(sink.Entries, Has.Count.EqualTo(1));

        LogManager.SetCategoryOverrides(new Dictionary<string, LogLevel>
        {
            ["Noisy"] = LogLevel.Error
        });

        DispatchTestMessage(LogLevel.Debug, "filtered", "Noisy");
        Assert.That(sink.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void WatchEnvironmentVariable_PicksUpChange()
    {
        Environment.SetEnvironmentVariable("TEST_LOG_LEVEL", "Warning");

        var sink = new RecordingSink();
        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
            c.WatchEnvironmentVariable("TEST_LOG_LEVEL", pollInterval: TimeSpan.FromMilliseconds(50));
        });

        // Wait for poll to pick up the env var
        Thread.Sleep(200);

        DispatchTestMessage(LogLevel.Debug, "filtered");
        Assert.That(sink.Entries, Has.Count.EqualTo(0));

        DispatchTestMessage(LogLevel.Warning, "visible");
        Assert.That(sink.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public void WatchConfigFile_PicksUpChange()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, """{"MinimumLevel": "Debug"}""");

            var sink = new RecordingSink();
            LogManager.Initialize(c =>
            {
                c.MinimumLevel = LogLevel.Debug;
                c.AddSink(sink);
                c.WatchConfigFile(tempFile);
            });

            DispatchTestMessage(LogLevel.Debug, "before");
            Assert.That(sink.Entries, Has.Count.EqualTo(1));

            // Update config file
            File.WriteAllText(tempFile, """{"MinimumLevel": "Error"}""");
            Thread.Sleep(1000); // Wait for debounce

            DispatchTestMessage(LogLevel.Debug, "filtered");
            Assert.That(sink.Entries, Has.Count.EqualTo(1));

            DispatchTestMessage(LogLevel.Error, "visible");
            Assert.That(sink.Entries, Has.Count.EqualTo(2));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Test]
    public void Reconfigure_DisposesOldMonitors()
    {
        Environment.SetEnvironmentVariable("TEST_LOG_LEVEL", "Error");

        LogManager.Initialize(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(new RecordingSink());
            c.WatchEnvironmentVariable("TEST_LOG_LEVEL", pollInterval: TimeSpan.FromMilliseconds(50));
        });

        // Wait for poll
        Thread.Sleep(200);

        // Reconfigure without monitor
        var sink = new RecordingSink();
        LogManager.Reconfigure(c =>
        {
            c.MinimumLevel = LogLevel.Debug;
            c.AddSink(sink);
        });

        // Old monitor should be disposed, env var shouldn't affect new config
        Thread.Sleep(200);

        DispatchTestMessage(LogLevel.Debug, "visible");
        Assert.That(sink.Entries, Has.Count.EqualTo(1));
    }

    private static void DispatchTestMessage(LogLevel level, string message, string category = "Test")
    {
        if (!LogManager.IsEnabled(level, category))
            return;

        var entry = new LogEntry(
            level: level,
            eventId: 1,
            timestampTicks: DateTime.UtcNow.Ticks,
            category: category);

        var utf8 = Encoding.UTF8.GetBytes(message).AsSpan();
        LogManager.Dispatch(in entry, utf8, 0, static (writer, state) => { });
    }
}
